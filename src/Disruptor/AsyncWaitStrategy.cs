using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor;

/// <summary>
/// Blocking wait strategy that uses <c>Monitor.Wait</c> and <c>Monitor.PulseAll</c>.
/// </summary>
/// <remarks>
/// This strategy can be used when throughput and low-latency are not as important as CPU resources.
/// </remarks>
public sealed class AsyncWaitStrategy : IAsyncWaitStrategy
{
    private readonly List<AsyncWaitState> _asyncWaitStates = new();
    private readonly object _gate = new();
    private bool _hasSyncWaiter;

    public bool IsBlockingStrategy => true;

    public SequenceWaitResult WaitFor(long sequence, DependentSequenceGroup dependentSequences, CancellationToken cancellationToken)
    {
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

    public void SignalAllWhenBlocking()
    {
        lock (_gate)
        {
            if (_hasSyncWaiter)
            {
                Monitor.PulseAll(_gate);
            }

            foreach (var completionSource in _asyncWaitStates)
            {
                completionSource.Signal();
            }
            _asyncWaitStates.Clear();
        }
    }

    public ValueTask<SequenceWaitResult> WaitForAsync(long sequence, AsyncWaitState asyncWaitState)
    {
        if (asyncWaitState.CursorValue < sequence)
        {
            lock (_gate)
            {
                if (asyncWaitState.CursorValue < sequence)
                {
                    asyncWaitState.ThrowIfCancellationRequested();

                    _asyncWaitStates.Add(asyncWaitState);

                    return asyncWaitState.Wait(sequence);
                }
            }
        }

        var availableSequence = asyncWaitState.GetAvailableSequence(sequence);

        return new ValueTask<SequenceWaitResult>(availableSequence);
    }
}
