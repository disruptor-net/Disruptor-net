using System;
using Disruptor.Dsl;
using Disruptor.Processing;

namespace Disruptor.Tests.Support;

public class TestEventProcessorFactory<T> : IEventProcessorFactory<T>
    where T : class
{
    private readonly Func<RingBuffer<T>, SequenceBarrier, IEventProcessor> _factory;

    public TestEventProcessorFactory(Func<RingBuffer<T>, SequenceBarrier, IEventProcessor> factory)
    {
        _factory = factory;
    }

    public SequenceBarrier? LastSequenceBarrier { get; private set; }

    public IEventProcessor CreateEventProcessor(RingBuffer<T> ringBuffer, SequenceBarrier sequenceBarrier)
    {
        LastSequenceBarrier = sequenceBarrier;

        return _factory.Invoke(ringBuffer, sequenceBarrier);
    }
}
