﻿using System;
using System.Threading;

namespace Disruptor;

/// <summary>
/// Non-blocking wait strategy that uses either <see cref="AggressiveSpinWait"/> or <see cref="SpinWait"/> depending
/// on the target <see cref="DependentSequenceGroup"/>.
/// </summary>
/// <remarks>
/// <para>
/// This strategy is a good option when the disruptor contains a chain of event handlers with different latency requirements.
/// The first handlers can be configured to use <see cref="AggressiveSpinWait"/>, while the following handlers will use
/// <see cref="SpinWait"/>.
/// </para>
/// <para>
/// This strategy uses the <see cref="DependentSequenceGroup"/> tag to apply the spin wait. It can be configured
/// using the disruptor:
/// <code>
/// disruptor.GetDependentSequencesFor(handler)!.Tag = HybridSpinWaitStrategy.AggressiveSpinWaitTag;
/// </code>
/// </para>
/// </remarks>
#pragma warning disable CS0618 // Type or member is obsolete
public class HybridSpinWaitStrategy : ISequenceWaitStrategy, IWaitStrategy
#pragma warning restore CS0618 // Type or member is obsolete
{
    /// <summary>
    /// Tag that identifies the <see cref="DependentSequenceGroup"/> for which <see cref="AggressiveSpinWait"/> should be used.
    /// </summary>
    public static object AggressiveSpinWaitTag { get; } = new();

    public bool IsBlockingStrategy { get; set; }

    public ISequenceWaiter NewSequenceWaiter(IEventHandler? eventHandler, DependentSequenceGroup dependentSequences)
    {
        return new SequenceWaiter(dependentSequences);
    }

    SequenceWaitResult IWaitStrategy.WaitFor(long sequence, DependentSequenceGroup dependentSequences, CancellationToken cancellationToken)
        => throw new NotSupportedException("IWaitStrategy must be converted to " + nameof(ISequenceWaitStrategy) + " before use.");

    public void SignalAllWhenBlocking()
    {
    }

    private class SequenceWaiter(DependentSequenceGroup dependentSequences) : ISequenceWaiter
    {
        public DependentSequenceGroup DependentSequences => dependentSequences;

        public SequenceWaitResult WaitFor(long sequence, CancellationToken cancellationToken)
        {
            return dependentSequences.Tag == AggressiveSpinWaitTag
                ? dependentSequences.AggressiveSpinWaitFor(sequence, cancellationToken)
                : dependentSequences.SpinWaitFor(sequence, cancellationToken);
        }

        public void Cancel()
        {
        }
    }
}
