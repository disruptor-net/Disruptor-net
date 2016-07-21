using System.Collections.Concurrent;
using System.Threading;

namespace Disruptor.PerfTests.Support
{
    public class ValueQueueProducer
    {
        private readonly Barrier _barrier;
        private readonly IProducerConsumerCollection<long> _blockingQueue;
        private readonly long _iterations;

        public ValueQueueProducer(Barrier barrier, IProducerConsumerCollection<long> blockingQueue, long iterations)
        {
            _barrier = barrier;
            _blockingQueue = blockingQueue;
            _iterations = iterations;
        }

        public void Run()
        {
            _barrier.SignalAndWait();
            for (long i = 0; i < _iterations; i++)
            {
                while (!_blockingQueue.TryAdd(i))
                    Thread.Yield();
            }
        }
    }
}
