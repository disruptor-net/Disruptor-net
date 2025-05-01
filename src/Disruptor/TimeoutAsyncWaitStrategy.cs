using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Disruptor;

/// <summary>
/// Blocking wait strategy that uses <c>Monitor.Wait</c> and <c>Monitor.PulseAll</c>.
/// If the awaited sequence is not available after the configured timeout, the strategy returns <see cref="SequenceWaitResult.Timeout"/>.
/// </summary>
/// <remarks>
/// Using a timeout wait strategy is only useful if your event handler handles timeouts (<see cref="IEventHandler.OnTimeout"/>).
///
/// This strategy can be used when throughput and low-latency are not as important as CPU resources.
/// </remarks>
public sealed class TimeoutAsyncWaitStrategy : IAsyncWaitStrategy
{
    private readonly object _gate = new();
    private readonly int _timeoutMilliseconds;
    private readonly List<SequenceWaiter> _waitList = [];
    private SequenceWaiter[] _sequenceWaiters = [];
    private bool _hasSyncWaiter;
    private int _asyncWaiterCount;
    private ThreadSate _threadState = new(false);

    public TimeoutAsyncWaitStrategy(TimeSpan timeout)
    {
        var totalMilliseconds = (long)timeout.TotalMilliseconds;
        if (totalMilliseconds is < 0 or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        _timeoutMilliseconds = (int)totalMilliseconds;
    }

    public bool IsBlockingStrategy => true;

    public ISequenceWaiter NewSequenceWaiter(SequenceWaiterOwner owner, DependentSequenceGroup dependentSequences)
    {
        _hasSyncWaiter = true;
        return new SequenceWaiter(this, dependentSequences);
    }

    public IAsyncSequenceWaiter NewAsyncSequenceWaiter(SequenceWaiterOwner owner, DependentSequenceGroup dependentSequences)
    {
        var sequenceWaiter = new SequenceWaiter(this, dependentSequences);
        OnAsyncWaiterAdded(sequenceWaiter);
        return sequenceWaiter;
    }

    public void SignalAllWhenBlocking()
    {
        lock (_gate)
        {
            if (_hasSyncWaiter)
            {
                Monitor.PulseAll(_gate);
            }

            foreach (var sequenceWaiter in _waitList)
            {
                if (sequenceWaiter.TimeoutDueTime != long.MaxValue)
                {
                    sequenceWaiter.TimeoutDueTime = long.MaxValue;
                    sequenceWaiter.Signal(true);
                }
            }

            _waitList.Clear();
        }
    }

    private void OnAsyncWaiterAdded(SequenceWaiter sequenceWaiter)
    {
        lock (_gate)
        {
            _sequenceWaiters = _sequenceWaiters.Append(sequenceWaiter).ToArray();
            _asyncWaiterCount++;
            if (_asyncWaiterCount == 1)
            {
                _threadState = new ThreadSate(true);

                var thread = new Thread(() => TimeoutProc(_threadState)) { IsBackground = true };
                thread.Start();

                _threadState.Started.Wait();
            }
        }
    }

    private void OnAsyncWaiterDisposed(SequenceWaiter sequenceWaiter)
    {
        lock (_gate)
        {
            _sequenceWaiters = _sequenceWaiters.Except([sequenceWaiter]).ToArray();
            _waitList.Remove(sequenceWaiter);
            _asyncWaiterCount--;
            if (_asyncWaiterCount == 0)
            {
                _threadState.IsRunning = false;
                _threadState = new ThreadSate(false);
            }
        }
    }

    private void TimeoutProc(ThreadSate state)
    {
        state.Started.Set();

        var maximumWaitTimeout = Math.Min(50, _timeoutMilliseconds);
        var waitTimeout = maximumWaitTimeout;

        while (state.IsRunning)
        {
            Thread.Sleep(waitTimeout);

            var now = TickCount64.Now();

            waitTimeout = ComputeWaitTimeout(_sequenceWaiters, now, maximumWaitTimeout);

            // Avoid taking the lock when there is no timeout to signal.
            if (waitTimeout > 0)
                continue;

            lock (_gate)
            {
                waitTimeout = maximumWaitTimeout;

                foreach (var sequenceWaiter in _waitList)
                {
                    var dueTime = sequenceWaiter.TimeoutDueTime;
                    if (dueTime == long.MaxValue)
                        continue;

                    var timeoutDelay = TickCount64.GetDelay(now, dueTime);
                    if (timeoutDelay <= 0)
                    {
                        sequenceWaiter.TimeoutDueTime = long.MaxValue;
                        sequenceWaiter.Signal(false);
                    }
                    else if (timeoutDelay < waitTimeout)
                    {
                        waitTimeout = timeoutDelay;
                    }
                }
            }
        }
    }

    private static int ComputeWaitTimeout(SequenceWaiter[] sequenceWaiters, long now, int maximumWaitTimeout)
    {
        var waitTimeout = maximumWaitTimeout;

        foreach (var sequenceWaiter in sequenceWaiters)
        {
            var dueTime = sequenceWaiter.TimeoutDueTime;
            if (dueTime == long.MaxValue)
                continue;

            var timeoutDelay = TickCount64.GetDelay(now, dueTime);
            if (timeoutDelay < waitTimeout)
            {
                waitTimeout = timeoutDelay;
            }
        }

        return waitTimeout;
    }

    private SequenceWaitResult WaitFor(SequenceWaiter waiter, long sequence, CancellationToken cancellationToken)
    {
        var timeout = _timeoutMilliseconds;
        if (waiter.DependentSequences.CursorValue < sequence)
        {
            lock (_gate)
            {
                while (waiter.DependentSequences.CursorValue < sequence)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var waitSucceeded = Monitor.Wait(_gate, timeout);
                    if (!waitSucceeded)
                    {
                        return SequenceWaitResult.Timeout;
                    }
                }
            }
        }

        return waiter.DependentSequences.AggressiveSpinWaitFor(sequence, cancellationToken);
    }

    private ValueTask<SequenceWaitResult> WaitForAsync(SequenceWaiter waiter, long sequence, CancellationToken cancellationToken)
    {
        if (waiter.CursorValue < sequence)
        {
            lock (_gate)
            {
                if (waiter.CursorValue < sequence)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    waiter.Reset();
                    waiter.TimeoutDueTime = TickCount64.Add(TickCount64.Now(), _timeoutMilliseconds);
                    _waitList.Add(waiter);

                    return waiter.Wait(sequence, cancellationToken);
                }
            }
        }

        var availableSequence = waiter.DependentSequences.AggressiveSpinWaitFor(sequence, cancellationToken);

        return new ValueTask<SequenceWaitResult>(availableSequence);
    }

    private class SequenceWaiter : ISequenceWaiter, IAsyncSequenceWaiter, IValueTaskSource<SequenceWaitResult>
    {
        private readonly TimeoutAsyncWaitStrategy _waitStrategy;
        private readonly DependentSequenceGroup _dependentSequences;
        private ManualResetValueTaskSourceCore<bool> _valueTaskSourceCore;
        private long _sequence;
        private CancellationToken _cancellationToken;
        private bool _disposed;
        private long _timeoutDueTime = long.MaxValue;

        public SequenceWaiter(TimeoutAsyncWaitStrategy waitStrategy, DependentSequenceGroup dependentSequences)
        {
            _waitStrategy = waitStrategy;
            _dependentSequences = dependentSequences;
            _valueTaskSourceCore = new() { RunContinuationsAsynchronously = true };
        }

        public long TimeoutDueTime
        {
            get => Volatile.Read(ref _timeoutDueTime);
            set => Volatile.Write(ref _timeoutDueTime, value);
        }

        public DependentSequenceGroup DependentSequences => _dependentSequences;

        public SequenceWaitResult WaitFor(long sequence, CancellationToken cancellationToken)
            => _waitStrategy.WaitFor(this, sequence, cancellationToken);

        public ValueTask<SequenceWaitResult> WaitForAsync(long sequence, CancellationToken cancellationToken)
            => _waitStrategy.WaitForAsync(this, sequence, cancellationToken);

        public void Cancel()
            => _waitStrategy.SignalAllWhenBlocking();

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _waitStrategy.OnAsyncWaiterDisposed(this);
        }

        public long CursorValue => _dependentSequences.CursorValue;

        public void Signal(bool success)
        {
            _valueTaskSourceCore.SetResult(success);
        }

        public void Reset()
        {
            _valueTaskSourceCore.Reset();
        }

        public ValueTask<SequenceWaitResult> Wait(long sequence, CancellationToken cancellationToken)
        {
            _sequence = sequence;
            _cancellationToken = cancellationToken;

            return new ValueTask<SequenceWaitResult>(this, _valueTaskSourceCore.Version);
        }

        public SequenceWaitResult GetResult(short token)
        {
            var success = _valueTaskSourceCore.GetResult(token);

            return success
                ? _dependentSequences.AggressiveSpinWaitFor(_sequence, _cancellationToken)
                : SequenceWaitResult.Timeout;
        }

        public ValueTaskSourceStatus GetStatus(short token)
        {
            return _valueTaskSourceCore.GetStatus(token);
        }

        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            _valueTaskSourceCore.OnCompleted(continuation, state, token, flags);
        }
    }

    private class ThreadSate
    {
        public ThreadSate(bool isRunning)
        {
            IsRunning = isRunning;
        }

        public volatile bool IsRunning;
        public readonly ManualResetEventSlim Started = new(false);
    }

    private static class TickCount64
    {
        public static long Now()
        {
#if NETSTANDARD2_1
            return Environment.TickCount;
#else
            return Environment.TickCount64;
#endif
        }

        public static long Add(long timeout, int offset)
        {
#if NETSTANDARD2_1
            return (int)timeout + offset;
#else
            return timeout + offset;
#endif
        }

        public static int GetDelay(long timeout1, long timeout2)
        {
#if NETSTANDARD2_1
            return (int)timeout2 - (int)timeout1;
#else
            return (int)(timeout2 - timeout1);
#endif
        }
    }
}
