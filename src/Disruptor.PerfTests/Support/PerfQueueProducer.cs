using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor.PerfTests.Support
{
    public class PerfQueueProducer
    {
        private readonly Barrier _barrier;
        private readonly ConcurrentQueue<long> _blockingQueue;
        private readonly long _iterations;

        public PerfQueueProducer(Barrier barrier, ConcurrentQueue<long> blockingQueue, long iterations)
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
                _blockingQueue.Enqueue(i);
            }
        }

        public Task Start()
        {
            return PerfTestUtil.StartLongRunning(Run);
        }
    }
}
