using Disruptor.Processing;

namespace Disruptor
{
    /// <summary>
    /// Implement this interface in your event handler to obtain the <see cref="IEventProcessor"/> sequence.
    ///
    /// Used by the <see cref="IEventProcessor"/> to set a callback allowing the event handler to notify
    /// when it has finished consuming an event if this happens after the OnEvent call.
    ///
    /// Typically this would be used when the handler is performing some sort of batching operation such as writing to an IO
    /// device; after the operation has completed, the implementation should set <see cref="Sequence.Value"/> to update the
    /// sequence and allow other processes that are dependent on this handler to progress.
    /// </summary>
    public interface IEventProcessorSequenceAware
    {
        /// <summary>
        /// Call by the <see cref="IEventProcessor"/> to setup the callback.
        /// </summary>
        /// <param name="sequenceCallback">callback on which to notify the <see cref="IEventProcessor"/> that the sequence has progressed.</param>
        void SetSequenceCallback(ISequence sequenceCallback);
    }
}
