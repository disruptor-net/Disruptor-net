﻿using System;
using System.Threading;

namespace Disruptor;

/// <summary>
/// Non-blocking wait strategy that uses a <see cref="SpinWait"/>.
/// </summary>
/// <remarks>
/// This strategy is a good compromise between performance and CPU resources.
/// Latency spikes can occur after quiet periods.
/// </remarks>
#pragma warning disable CS0618 // Type or member is obsolete
public sealed class SpinWaitWaitStrategy : ISequenceWaitStrategy, IWaitStrategy
#pragma warning restore CS0618 // Type or member is obsolete
{
    public bool IsBlockingStrategy => false;

    public ISequenceWaiter NewSequenceWaiter(IEventHandler? eventHandler, DependentSequenceGroup dependentSequences)
    {
        return new SequenceWaiter(dependentSequences);
    }

    public SequenceWaitResult WaitFor(long sequence, DependentSequenceGroup dependentSequences, CancellationToken cancellationToken)
        => throw new NotSupportedException("IWaitStrategy must be converted to " + nameof(ISequenceWaitStrategy) + " before use.");

    public void SignalAllWhenBlocking()
    {
    }

    private class SequenceWaiter(DependentSequenceGroup dependentSequences) : ISequenceWaiter
    {
        public DependentSequenceGroup DependentSequences => dependentSequences;

        public SequenceWaitResult WaitFor(long sequence, CancellationToken cancellationToken)
        {
            return dependentSequences.SpinWaitFor(sequence, cancellationToken);
        }

        public void Cancel()
        {
        }

        public void Dispose()
        {
        }
    }
}
