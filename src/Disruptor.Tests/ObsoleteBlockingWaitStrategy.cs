using System;
using System.Threading;
#pragma warning disable CS0618 // Type or member is obsolete

namespace Disruptor.Tests;

/// <summary>
/// Previous implementation of the <see cref="BlockingWaitStrategy"/>.
/// Required to ensure <see cref="IWaitStrategy"/> can still be used to implement wait strategies.
/// </summary>
public class ObsoleteBlockingWaitStrategy : IWaitStrategy
{
    private readonly object _gate = new();

    public bool IsBlockingStrategy => true;

    public SequenceWaitResult WaitFor(long sequence, DependentSequenceGroup dependentSequences, CancellationToken cancellationToken)
    {
        if (dependentSequences.CursorValue < sequence)
        {
            lock (_gate)
            {
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
            Monitor.PulseAll(_gate);
        }
    }
}
