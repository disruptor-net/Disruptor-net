using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor;

/// <summary>
/// Blocking wait strategy that uses <c>Monitor.Wait</c> and <c>Monitor.PulseAll</c>.
/// If the awaited sequence is not available after the configured timeout, the strategy returns <see cref="SequenceWaitResult.Timeout"/>.
/// </summary>
/// <remarks>
/// Using a timeout wait strategy is only useful if your event handler handles timeouts (<see cref="IEventHandler{T}.OnTimeout"/>,
/// <see cref="IValueEventHandler{T}.OnTimeout"/>, <see cref="IBatchEventHandler{T}.OnTimeout"/> or <see cref="IAsyncBatchEventHandler{T}.OnTimeout"/>).
///
/// This strategy can be used when throughput and low-latency are not as important as CPU resources.
/// </remarks>
public sealed class TimeoutAsyncWaitStrategy : IAsyncWaitStrategy
{
    private readonly List<TaskCompletionSource<bool>> _taskCompletionSources = new();
    private readonly object _gate = new();
    private readonly int _timeoutMilliseconds;
    private bool _hasSyncWaiter;

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

    public SequenceWaitResult WaitFor(long sequence, DependentSequenceGroup dependentSequences, CancellationToken cancellationToken)
    {
        var timeout = _timeoutMilliseconds;
        if (dependentSequences.CursorValue < sequence)
        {
            lock (_gate)
            {
                _hasSyncWaiter = true;
                while (dependentSequences.CursorValue < sequence)
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

        return dependentSequences.AggressiveSpinWaitFor(sequence, cancellationToken);
    }

    public void SignalAllWhenBlocking()
    {
        lock (_gate)
        {
            if (_hasSyncWaiter)
            {
                Monitor.PulseAll(_gate);
            }

            foreach (var completionSource in _taskCompletionSources)
            {
                completionSource.TrySetResult(true);
            }
            _taskCompletionSources.Clear();
        }
    }

    public async ValueTask<SequenceWaitResult> WaitForAsync(long sequence, AsyncWaitState asyncWaitState)
    {
        while (asyncWaitState.CursorValue < sequence)
        {
            var waitSucceeded = await WaitForAsyncImpl(sequence, asyncWaitState).ConfigureAwait(false);
            if (!waitSucceeded)
            {
                return SequenceWaitResult.Timeout;
            }
        }

        return asyncWaitState.AggressiveSpinWaitFor(sequence);
    }

    private async ValueTask<bool> WaitForAsyncImpl(long sequence, AsyncWaitState asyncWaitState)
    {
        TaskCompletionSource<bool> tcs;

        lock (_gate)
        {
            if (asyncWaitState.CursorValue >= sequence)
            {
                return true;
            }

            asyncWaitState.ThrowIfCancellationRequested();

            tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _taskCompletionSources.Add(tcs);
        }

        using (var cts = new CancellationTokenSource())
        {
            var delayTask = Task.Delay(_timeoutMilliseconds, cts.Token);

            var resultTask = await Task.WhenAny(tcs.Task, delayTask).ConfigureAwait(false);
            if (resultTask == delayTask)
            {
                return false;
            }

            // Cancel the timer task so that it does not fire
            cts.Cancel();

            // tcs.Task is not awaited because it cannot possibly throw

            return true;
        }
    }
}
