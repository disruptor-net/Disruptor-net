using System;
using Disruptor.Dsl;
using Disruptor.Processing;

namespace Disruptor.Tests.Support;

public class TestValueEventProcessorFactory<T> : IValueEventProcessorFactory<T>
    where T : struct
{
    private readonly Func<IValueRingBuffer<T>, SequenceBarrier, IEventProcessor> _factory;

    public TestValueEventProcessorFactory(Func<IValueRingBuffer<T>, SequenceBarrier, IEventProcessor> factory)
    {
        _factory = factory;
    }

    public SequenceBarrier? LastSequenceBarrier { get; private set; }

    public IEventProcessor CreateEventProcessor(IValueRingBuffer<T> ringBuffer, SequenceBarrier sequenceBarrier)
    {
        LastSequenceBarrier = sequenceBarrier;

        return _factory.Invoke(ringBuffer, sequenceBarrier);
    }
}
