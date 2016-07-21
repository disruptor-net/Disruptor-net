using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;
using Disruptor.PerfTests.WorkHandler;

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
    /// EP1 - EventeProcessor 1
    /// </summary>
    class OneToOneQueueThroughputTest : IThroughputTest, IQueueTest
    {
        private const int _bufferSize = 1024*64;
        private const long _iterations = 1000L*1000L*10L;
        private const long _expectedResult = _iterations*3L;

        private readonly IProducerConsumerCollection<long> _blockingQueue = new LockFreeBoundedQueue<long>(_bufferSize);
        private readonly ValueAdditionQueueProcessor _queueProcessor;

        public OneToOneQueueThroughputTest()
        {
            _queueProcessor = new ValueAdditionQueueProcessor(_blockingQueue, _iterations - 1);
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
