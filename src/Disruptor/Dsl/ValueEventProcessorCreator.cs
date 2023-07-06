using Disruptor.Processing;

namespace Disruptor.Dsl;

/// <summary>
/// A factory to make it possible to include custom event processors in a chain:
///
/// <code>disruptor.HandleEventsWith(handler1).Then((ringBuffer, sequenceBarrier) => new CustomEventProcessor(ringBuffer, sequenceBarrier));</code>
/// </summary>
/// <param name="ringBuffer">the event ring buffer</param>
/// <param name="sequenceBarrier">coordination barrier that should be used to wait for events</param>
/// <returns>a new event processor that gates on <paramref name="sequenceBarrier"/> before processing events</returns>
public delegate IEventProcessor ValueEventProcessorCreator<T>(IValueRingBuffer<T> ringBuffer, SequenceBarrier sequenceBarrier)
    where T : struct;
