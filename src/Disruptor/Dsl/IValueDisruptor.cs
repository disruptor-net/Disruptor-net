namespace Disruptor.Dsl;

internal interface IValueDisruptor<T>
    where T : struct
{
    IValueRingBuffer<T> RingBuffer { get; }

    ValueEventHandlerGroup<T> CreateEventProcessors(Sequence[] barrierSequences, IValueEventHandler<T>[] eventHandlers);
    ValueEventHandlerGroup<T> CreateEventProcessors(Sequence[] barrierSequences, ValueEventProcessorCreator<T>[] processorFactories);
}
