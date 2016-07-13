using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Sequenced;
using Disruptor.PerfTests.Support;

namespace Disruptor.PerfTests.Queue
{
    public class OneToOneQueueBatchedThroughputTest : IPerfTest
    {
        private const int BufferSize = 1024 * 64;
        private const long Iterations = 1000L * 1000L * 10L;
        private const long _expectedResult = Iterations * 3L;

        private readonly BlockingCollection<long> _blockingQueue = new BlockingCollection<long>(new ConcurrentQueue<long>(), BufferSize);
        private readonly ValueAdditionBatchQueueProcessor _queueProcessor;
        
        public OneToOneQueueBatchedThroughputTest()
        {
            _queueProcessor = new ValueAdditionBatchQueueProcessor(_blockingQueue, Iterations);
        }

        public int RequiredProcessorCount => 2;

        public long Run(Stopwatch stopwatch)
        {
            var latch = new ManualResetEvent(false);
            _queueProcessor.Reset(latch);
            var future = Task.Run(() => _queueProcessor.Run());
            stopwatch.Start();

            for (long i = 0; i < Iterations; i++)
            {
                _blockingQueue.Add(3L);
            }

            latch.WaitOne();
            stopwatch.Stop();
            _queueProcessor.Halt();
            future.Wait();

            PerfTestUtil.FailIf(_expectedResult, 0);

            return Iterations;
        }
    }
}
