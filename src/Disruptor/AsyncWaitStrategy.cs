using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor;

/// <summary>
/// Blocking wait strategy that uses <c>Monitor.Wait</c> for event processors waiting on a barrier.
///
/// Can be configured to generate timeouts. Activating timeouts is only useful if your event handler
/// handles timeouts (<see cref="IAsyncBatchEventHandler{T}.OnTimeout"/>).
/// </summary>
/// <remarks>
/// This strategy can be used when throughput and low-latency are not as important as CPU resources.
/// </remarks>
public sealed class AsyncWaitStrategy : IAsyncWaitStrategy
{
    private readonly List<TaskCompletionSource<bool>> _taskCompletionSources = new();
    private readonly object _gate = new();
    private readonly int _timeoutMilliseconds;
    private bool _hasSyncWaiter;

    /// <summary>
    /// Creates an async wait strategy without timeouts.
    /// </summary>
    public AsyncWaitStrategy()
        : this(Timeout.Infinite)
    {
    }

    /// <summary>
    /// Creates an async wait strategy with timeouts.
    /// </summary>
    public AsyncWaitStrategy(TimeSpan timeout)
        : this(ToMilliseconds(timeout))
    {
    }

    private AsyncWaitStrategy(int timeoutMilliseconds)
    {
        _timeoutMilliseconds = timeoutMilliseconds;
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

    public async ValueTask<SequenceWaitResult> WaitForAsync(long sequence, DependentSequenceGroup dependentSequences, CancellationToken cancellationToken)
    {
        while (dependentSequences.CursorValue < sequence)
        {
            var waitSucceeded = await WaitForAsyncImpl(sequence, dependentSequences, cancellationToken).ConfigureAwait(false);
            if (!waitSucceeded)
            {
                return SequenceWaitResult.Timeout;
            }
        }

        return dependentSequences.AggressiveSpinWaitFor(sequence, cancellationToken);
    }

    private async ValueTask<bool> WaitForAsyncImpl(long sequence, DependentSequenceGroup dependentSequences, CancellationToken cancellationToken)
    {
        TaskCompletionSource<bool> tcs;

        lock (_gate)
        {
            if (dependentSequences.CursorValue >= sequence)
            {
                return true;
            }

            cancellationToken.ThrowIfCancellationRequested();

            tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _taskCompletionSources.Add(tcs);
        }

        // Using cancellationToken in the await is not required because SignalAllWhenBlocking is always invoked by
        // the sequencer barrier after cancellation.

        await AddTimeout(tcs.Task).ConfigureAwait(false);

        return tcs.Task.IsCompleted;
    }

    private Task AddTimeout(Task task)
    {
        if (_timeoutMilliseconds == Timeout.Infinite)
        {
            return task;
        }

        return Task.WhenAny(task, Task.Delay(_timeoutMilliseconds));
    }

    private static int ToMilliseconds(TimeSpan timeout)
    {
        var totalMilliseconds = (long)timeout.TotalMilliseconds;
        if (totalMilliseconds is < 0 or >= int.MaxValue)
        {
            throw new ArgumentOutOfRangeException();
        }

        return (int)totalMilliseconds;
    }
}
