using Disruptor.Processing;

namespace Disruptor.Dsl;

/// <summary>
/// A factory interface to make it possible to include custom event processors in a chain:
///
/// <code>disruptor.HandleEventsWith(handler1).Then((ringBuffer, sequenceBarrier) -> new CustomEventProcessor(ringBuffer, sequenceBarrier));</code>
/// </summary>
public interface IEventProcessorFactory<T> where T : class
{
    /// <summary>
    /// Create a new event processor that gates on <paramref name="sequenceBarrier"/>.
    /// </summary>
    /// <param name="ringBuffer">the event ring buffer</param>
    /// <param name="sequenceBarrier">coordination barrier that should be used to wait for events</param>
    /// <returns>a new event processor that gates on <paramref name="sequenceBarrier"/> before processing events</returns>
    IEventProcessor CreateEventProcessor(RingBuffer<T> ringBuffer, SequenceBarrier sequenceBarrier);
}
