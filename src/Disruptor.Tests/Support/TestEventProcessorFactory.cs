using System;
using Disruptor.Dsl;

namespace Disruptor.Tests.Support
{
    public class TestEventProcessorFactory<T> : IEventProcessorFactory<T>
        where T : class
    {
        private readonly Func<RingBuffer<T>, ISequence[], IEventProcessor> _factory;

        public TestEventProcessorFactory(Func<RingBuffer<T>, ISequence[], IEventProcessor> factory)
        {
            _factory = factory;
        }

        public IEventProcessor CreateEventProcessor(RingBuffer<T> ringBuffer, ISequence[] barrierSequences)
        {
            return _factory.Invoke(ringBuffer, barrierSequences);
        }
    }
}
