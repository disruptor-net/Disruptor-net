using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
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

        private readonly BlockingCollection<long> _blockingQueue = new BlockingCollection<long>(BufferSize);
        private readonly ValueAdditionQueueProcessor _queueProcessor;
        private static readonly ConcurrentQueue<long> _concurrentQueue = new ConcurrentQueue<long>();

        public OneToOneQueueThroughputTest()
        {
            _queueProcessor = new ValueAdditionQueueProcessor(_blockingQueue, Iterations - 1);
            foreach (var i in Enumerable.Range(0, BufferSize))
            {
                _concurrentQueue.Enqueue(i);
            }
            while (!_concurrentQueue.IsEmpty)
            {
                long value;
                _concurrentQueue.TryDequeue(out value);
            }
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
