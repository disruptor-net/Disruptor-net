using System;
using Disruptor.Dsl;

namespace Disruptor.Tests.Support
{
    public class TestValueEventProcessorFactory<T> : IValueEventProcessorFactory<T>
        where T : struct
    {
        private readonly Func<IValueRingBuffer<T>, ISequence[], IEventProcessor> _factory;

        public TestValueEventProcessorFactory(Func<IValueRingBuffer<T>, ISequence[], IEventProcessor> factory)
        {
            _factory = factory;
        }

        public IEventProcessor CreateEventProcessor(IValueRingBuffer<T> ringBuffer, ISequence[] barrierSequences)
        {
            return _factory.Invoke(ringBuffer, barrierSequences);
        }
    }
}
