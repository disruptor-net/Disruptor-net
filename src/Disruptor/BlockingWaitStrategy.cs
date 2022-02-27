﻿using System.Threading;

namespace Disruptor;

/// <summary>
/// Blocking strategy that uses <c>Monitor.Wait</c> for event processors waiting on a barrier.
/// </summary>
/// <remarks>
/// This strategy can be used when throughput and low-latency are not as important as CPU resources.
/// This strategy busy spins when waiting for the dependent sequence, which can generate CPU spikes.
///
/// Consider using <see cref="BlockingSpinWaitWaitStrategy"/> to avoid CPU spikes.
/// </remarks>
public sealed class BlockingWaitStrategy : IWaitStrategy
{
    private readonly object _gate = new();

    public bool IsBlockingStrategy => true;

    public SequenceWaitResult WaitFor(long sequence, Sequence cursor, ISequence dependentSequence, CancellationToken cancellationToken)
    {
        if (cursor.Value < sequence)
        {
            Wait(sequence, cursor, cancellationToken);
        }

        return dependentSequence.AggressiveSpinWaitFor(sequence, cancellationToken);
    }

    private void Wait(long sequence, Sequence cursor, CancellationToken cancellationToken)
    {
        var spinCount = AggressiveSpinWait.SpinCountSpinBeforeWait;
        var spinWait = new AggressiveSpinWait();
        while (spinWait.Count < spinCount)
        {
            spinWait.SpinOnce();
            if (cursor.Value >= sequence)
            {
                return;
            }
        }

        lock (_gate)
        {
            while (cursor.Value < sequence)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Monitor.Wait(_gate);
            }
        }
    }

    public void SignalAllWhenBlocking()
    {
        lock (_gate)
        {
            Monitor.PulseAll(_gate);
        }
    }
}
