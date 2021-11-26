using System;
using Disruptor.Internal;

namespace Disruptor.Processing
{
    /// <summary>
    /// Factory that creates optimized instance of <see cref="ProcessingSequenceBarrier{TSequencer,TWaitStrategy}"/>.
    /// </summary>
    internal static class ProcessingSequenceBarrierFactory
    {
        /// <summary>
        /// Create a new <see cref="ProcessingSequenceBarrier{TSequencer,TWaitStrategy}"/> with dedicated generic arguments.
        /// </summary>
        public static ISequenceBarrier Create(ISequencer sequencer, IWaitStrategy waitStrategy, Sequence cursorSequence, ISequence[] dependentSequences)
        {
            var sequencerProxy = StructProxy.CreateProxyInstance(sequencer);
            var waitStrategyProxy = StructProxy.CreateProxyInstance(waitStrategy);

            var sequencerBarrierType = typeof(ProcessingSequenceBarrier<,>).MakeGenericType(sequencerProxy.GetType(), waitStrategyProxy.GetType());
            return (ISequenceBarrier)Activator.CreateInstance(sequencerBarrierType, sequencerProxy, waitStrategyProxy, cursorSequence, dependentSequences);
        }
    }
}
