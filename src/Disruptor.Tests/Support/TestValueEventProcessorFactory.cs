using System;
using Disruptor.Dsl;

namespace Disruptor.Tests.Support
{
    public class TestValueEventProcessorFactory<T> : IValueEventProcessorFactory<T>
        where T : struct
    {
        private readonly Func<ValueRingBuffer<T>, ISequence[], IEventProcessor> _factory;

        public TestValueEventProcessorFactory(Func<ValueRingBuffer<T>, ISequence[], IEventProcessor> factory)
        {
            _factory = factory;
        }

        public IEventProcessor CreateEventProcessor(ValueRingBuffer<T> ringBuffer, ISequence[] barrierSequences)
        {
            return _factory.Invoke(ringBuffer, barrierSequences);
        }
    }
}
