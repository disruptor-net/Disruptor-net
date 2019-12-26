namespace Disruptor.Dsl
{
    internal interface IValueDisruptor<T>
        where T : struct
    {
        IValueRingBuffer<T> RingBuffer { get; }

        ValueEventHandlerGroup<T> CreateEventProcessors(ISequence[] barrierSequences, IValueEventHandler<T>[] eventHandlers);
        ValueEventHandlerGroup<T> CreateEventProcessors(ISequence[] barrierSequences, IValueEventProcessorFactory<T>[] processorFactories);
    }
}
