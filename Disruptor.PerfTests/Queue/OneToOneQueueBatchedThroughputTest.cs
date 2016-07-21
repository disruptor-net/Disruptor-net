using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;

namespace Disruptor.PerfTests.Queue
{
    public class OneToOneQueueBatchedThroughputTest : IThroughputTest, IQueueTest
    {
        private const int _bufferSize = 1024 * 64;
        private const long _iterations = 1000L * 1000L * 10L;
        private const long _expectedResult = _iterations * 3L;

        private readonly IProducerConsumerCollection<long> _blockingQueue = new ConcurrentQueue<long>();
        private readonly ValueAdditionBatchQueueProcessor _queueProcessor;
        
        public OneToOneQueueBatchedThroughputTest()
        {
            _queueProcessor = new ValueAdditionBatchQueueProcessor(_blockingQueue, _iterations);
        }

        public int RequiredProcessorCount => 2;

        public long Run(Stopwatch stopwatch)
        {
            var latch = new ManualResetEvent(false);
            _queueProcessor.Reset(latch);
            var future = Task.Run(() => _queueProcessor.Run());
            stopwatch.Start();

            for (long i = 0; i < _iterations; i++)
            {
                while (!_blockingQueue.TryAdd(3L))
                    Thread.Yield();
            }

            latch.WaitOne();
            stopwatch.Stop();
            _queueProcessor.Halt();
            future.Wait();

            PerfTestUtil.FailIf(_expectedResult, 0);

            return _iterations;
        }
    }
}
