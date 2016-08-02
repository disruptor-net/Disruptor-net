using Disruptor.Tests.Support;

namespace Disruptor.Tests.Dsl.Stubs
{
    public class StubPublisher
    {
        private volatile bool _running = true;
        private volatile int _publicationCount;

        private readonly RingBuffer<TestEvent> _ringBuffer;

        public StubPublisher(RingBuffer<TestEvent> ringBuffer)
        {
            _ringBuffer = ringBuffer;
        }

        public void Run()
        {
            while (_running)
            {
                var sequence = _ringBuffer.Next();
                //TestEvent entry = ringBuffer.get(sequence);
                _ringBuffer.Publish(sequence);
                _publicationCount++;
            }
        }

        public int GetPublicationCount()
        {
            return _publicationCount;
        }

        public void Halt()
        {
            _running = false;
        }
    }
}