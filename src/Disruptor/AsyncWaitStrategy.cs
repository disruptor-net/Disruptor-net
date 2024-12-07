using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Disruptor;

/// <summary>
/// Blocking wait strategy that uses <c>Monitor.Wait</c> and <c>Monitor.PulseAll</c>.
/// </summary>
/// <remarks>
/// This strategy can be used when throughput and low-latency are not as important as CPU resources.
/// </remarks>
public sealed class AsyncWaitStrategy : IAsyncSequenceWaitStrategy
{
    private readonly List<SequenceWaiter> _waiters = new();
    private readonly object _gate = new();
    private bool _hasSyncWaiter;

    public bool IsBlockingStrategy => true;

    public ISequenceWaiter NewSequenceWaiter(IEventHandler? eventHandler, DependentSequenceGroup dependentSequences)
        => new SequenceWaiter(this, dependentSequences);

    public IAsyncSequenceWaiter NewAsyncSequenceWaiter(IEventHandler? eventHandler, DependentSequenceGroup dependentSequences)
        => new SequenceWaiter(this, dependentSequences);

    public void SignalAllWhenBlocking()
    {
        lock (_gate)
        {
            if (_hasSyncWaiter)
            {
                Monitor.PulseAll(_gate);
            }

            foreach (var waiter in _waiters)
            {
                waiter.Signal();
            }
            _waiters.Clear();
        }
    }

    private SequenceWaitResult WaitFor(SequenceWaiter waiter, long sequence, CancellationToken cancellationToken)
    {
        var dependentSequences = waiter.DependentSequences;
        if (dependentSequences.CursorValue < sequence)
        {
            lock (_gate)
            {
                _hasSyncWaiter = true;
                while (dependentSequences.CursorValue < sequence)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Monitor.Wait(_gate);
                }
            }
        }

        return dependentSequences.AggressiveSpinWaitFor(sequence, cancellationToken);
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

                    _waiters.Add(waiter);

                    return waiter.Wait(sequence, cancellationToken);
                }
            }
        }

        var availableSequence = waiter.DependentSequences.AggressiveSpinWaitFor(sequence, cancellationToken);

        return new ValueTask<SequenceWaitResult>(availableSequence);
    }

    private class SequenceWaiter : ISequenceWaiter, IAsyncSequenceWaiter
    {
        private readonly ValueTaskSource _valueTaskSource;
        private readonly AsyncWaitStrategy _waitStrategy;
        private readonly DependentSequenceGroup _dependentSequences;
        private ManualResetValueTaskSourceCore<bool> _valueTaskSourceCore;
        private long _sequence;
        private CancellationToken _cancellationToken;

        public SequenceWaiter(AsyncWaitStrategy waitStrategy, DependentSequenceGroup dependentSequences)
        {
            _valueTaskSource = new(this);
            _waitStrategy = waitStrategy;
            _dependentSequences = dependentSequences;
            _valueTaskSourceCore = new() { RunContinuationsAsynchronously = true };
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
        }

        public long CursorValue => _dependentSequences.CursorValue;

        public void Signal()
        {
            _valueTaskSourceCore.SetResult(true);
        }

        public ValueTask<SequenceWaitResult> Wait(long sequence, CancellationToken cancellationToken)
        {
            _valueTaskSourceCore.Reset();
            _sequence = sequence;
            _cancellationToken = cancellationToken;

            return new ValueTask<SequenceWaitResult>(_valueTaskSource, _valueTaskSourceCore.Version);
        }

        private SequenceWaitResult GetResult(short token)
        {
            _valueTaskSourceCore.GetResult(token);

            return _dependentSequences.AggressiveSpinWaitFor(_sequence, _cancellationToken);
        }

        private ValueTaskSourceStatus GetStatus(short token)
        {
            return _valueTaskSourceCore.GetStatus(token);
        }

        private void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            _valueTaskSourceCore.OnCompleted(continuation, state, token, flags);
        }

        private class ValueTaskSource : IValueTaskSource<SequenceWaitResult>
        {
            private readonly SequenceWaiter _asyncWaitState;

            public ValueTaskSource(SequenceWaiter asyncWaitState)
            {
                _asyncWaitState = asyncWaitState;
            }

            public SequenceWaitResult GetResult(short token)
            {
                return _asyncWaitState.GetResult(token);
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                return _asyncWaitState.GetStatus(token);
            }

            public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            {
                _asyncWaitState.OnCompleted(continuation, state, token, flags);
            }
        }
    }
}
