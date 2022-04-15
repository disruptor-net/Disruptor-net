using System;
using Disruptor.Util;

namespace Disruptor.Processing;

/// <summary>
/// Factory that creates optimized instance of sequence barriers.
/// </summary>
public static class ProcessingSequenceBarrierFactory
{
    /// <summary>
    /// Create a new <see cref="ProcessingSequenceBarrier{TSequencer,TWaitStrategy}"/> with dedicated generic arguments.
    /// </summary>
    public static ISequenceBarrier Create(ISequencer sequencer, IWaitStrategy waitStrategy, Sequence cursorSequence, ISequence[] dependentSequences)
    {
        var sequencerProxy = StructProxy.CreateProxyInstance(sequencer);
        var waitStrategyProxy = StructProxy.CreateProxyInstance(waitStrategy);

        var sequencerBarrierType = typeof(ProcessingSequenceBarrier<,>).MakeGenericType(sequencerProxy.GetType(), waitStrategyProxy.GetType());
        return (ISequenceBarrier)Activator.CreateInstance(sequencerBarrierType, sequencerProxy, waitStrategyProxy, cursorSequence, dependentSequences)!;
    }

    /// <summary>
    /// Create a new <see cref="AsyncProcessingSequenceBarrier{TSequencer,TWaitStrategy}"/> with dedicated generic arguments.
    /// </summary>
    public static IAsyncSequenceBarrier CreateAsync(ISequencer sequencer, IWaitStrategy waitStrategy, Sequence cursorSequence, ISequence[] dependentSequences)
    {
        if (waitStrategy is not IAsyncWaitStrategy asyncWaitStrategy)
            throw new InvalidOperationException($"The disruptor must be configured with an async wait strategy (e.g.: {nameof(AsyncWaitStrategy)}");

        return CreateAsync(sequencer, asyncWaitStrategy, cursorSequence, dependentSequences);
    }

    /// <summary>
    /// Create a new <see cref="AsyncProcessingSequenceBarrier{TSequencer,TWaitStrategy}"/> with dedicated generic arguments.
    /// </summary>
    public static IAsyncSequenceBarrier CreateAsync(ISequencer sequencer, IAsyncWaitStrategy asyncWaitStrategy, Sequence cursorSequence, ISequence[] dependentSequences)
    {
        var sequencerProxy = StructProxy.CreateProxyInstance(sequencer);
        var waitStrategyProxy = StructProxy.CreateProxyInstance(asyncWaitStrategy);

        var sequencerBarrierType = typeof(AsyncProcessingSequenceBarrier<,>).MakeGenericType(sequencerProxy.GetType(), waitStrategyProxy.GetType());
        return (IAsyncSequenceBarrier)Activator.CreateInstance(sequencerBarrierType, sequencerProxy, waitStrategyProxy, cursorSequence, dependentSequences)!;
    }
}
