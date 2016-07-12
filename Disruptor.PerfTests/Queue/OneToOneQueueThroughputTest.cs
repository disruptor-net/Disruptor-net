using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;

namespace Disruptor.PerfTests.Queue
{
    /// <summary>
    /// UniCast a series of items between 1 publisher and 1 event processor.
    /// 
    /// +----+    +-----+
    /// | P1 |--->| EP1 |
    /// +----+    +-----+
    /// 
    /// Queue Based:
    /// ============
    ///        put      take
    /// +----+    +====+    +-----+
    /// | P1 |--->| Q1 |/---| EP1 |
    /// +----+    +====+    +-----+
    /// 
    /// P1  - Publisher 1
    /// Q1  - Queue 1
    /// EP1 - EventProcessor 1
    /// </summary>
    class OneToOneQueueThroughputTest : IPerfTest
    {
        private const int BufferSize = 1024*64;
        private const long Iterations = 1000L*1000L*10L;
        private const long _expectedResult = Iterations*3L;

        private readonly BlockingCollection<long> _blockingQueue = new BlockingCollection<long>(new ConcurrentQueue<long>(), BufferSize);
        private readonly ValueAdditionQueueProcessor _queueProcessor;

        public OneToOneQueueThroughputTest()
        {
            _queueProcessor = new ValueAdditionQueueProcessor(_blockingQueue, Iterations - 1);
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
            future.Dispose();

            PerfTestUtil.FailIf(_expectedResult, 0);

            return Iterations;
        }

    }
}
