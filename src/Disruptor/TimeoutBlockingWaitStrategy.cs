using System;
using System.Threading;
using Disruptor.Processing;

namespace Disruptor;

/// <summary>
/// Blocking strategy that uses a lock and condition variable for <see cref="IEventProcessor"/> waiting on a barrier.
/// However it will periodically wake up if it has been idle for specified period by returning <see cref="SequenceWaitResult.Timeout"/>.
///
/// Using a timeout wait strategy is only useful if your event handler handles timeouts (<see cref="IEventHandler{T}.OnTimeout"/>,
/// <see cref="IValueEventHandler{T}.OnTimeout"/> or <see cref="IBatchEventHandler{T}.OnTimeout"/>).
/// </summary>
/// <remarks>
/// This strategy can be used when throughput and low-latency are not as important as CPU resources.
/// </remarks>
public sealed class TimeoutBlockingWaitStrategy : IWaitStrategy
{
    private readonly object _gate = new();
    private readonly TimeSpan _timeout;

    public TimeoutBlockingWaitStrategy(TimeSpan timeout)
    {
        _timeout = timeout;
    }

    public bool IsBlockingStrategy => true;

    public SequenceWaitResult WaitFor(long sequence, Sequence cursor, ISequence dependentSequence, CancellationToken cancellationToken)
    {
        var timeSpan = _timeout;
        if (cursor.Value < sequence)
        {
            lock (_gate)
            {
                while (cursor.Value < sequence)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!Monitor.Wait(_gate, timeSpan))
                    {
                        return SequenceWaitResult.Timeout;
                    }
                }
            }
        }

        var aggressiveSpinWait = new AggressiveSpinWait();
        long availableSequence;
        while ((availableSequence = dependentSequence.Value) < sequence)
        {
            cancellationToken.ThrowIfCancellationRequested();
            aggressiveSpinWait.SpinOnce();
        }

        return availableSequence;
    }

    public void SignalAllWhenBlocking()
    {
        lock (_gate)
        {
            Monitor.PulseAll(_gate);
        }
    }
}
