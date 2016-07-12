using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.PerfTests.Support;

namespace Disruptor.PerfTests.Sequenced
{
    /// <summary>
    /// UniCast a series of items between 1 publisher and 1 event processor
    /// 
    /// <code>
    /// +----+    +-----+
    /// | P1 |--->| EP1 |
    /// +----+    +-----+
    /// 
    /// Disruptor:
    /// ==========
    ///              track to prevent wrap
    ///              +------------------+
    ///              |                  |
    ///              |                  v
    /// +----+    +====+    +====+   +-----+
    /// | P1 |---›| RB |‹---| SB |   | EP1 |
    /// +----+    +====+    +====+   +-----+
    ///      claim      get    ^        |
    ///                        |        |
    ///                        +--------+
    ///                          waitFor
    /// 
    /// P1  - Publisher 1
    /// RB  - RingBuffer
    /// SB  - SequenceBarrier
    /// EP1 - EventProcessor 1
    /// </code>
    /// </summary>
    public class OneToOneSequencedBatchThroughputTest : IThroughputfTest
    {
        private const int _batchSize = 10;
        private const int _bufferSize = 1024 * 64;
        private const long _iterations = 1000L * 1000L * 100L;
        private readonly IExecutor _executor = new BasicExecutor(TaskScheduler.Current);
        private static readonly long _expectedResult = PerfTestUtil.AccumulatedAddition(_iterations) * _batchSize;

        ///////////////////////////////////////////////////////////////////////////////////////////////

        private readonly RingBuffer<ValueEvent> _ringBuffer;
        private readonly ValueAdditionEventHandler _handler;
        private readonly BatchEventProcessor<ValueEvent> _batchEventProcessor;

        public OneToOneSequencedBatchThroughputTest()
        {
            _ringBuffer = RingBuffer<ValueEvent>.CreateSingleProducer(() => new ValueEvent(), _bufferSize, new YieldingWaitStrategy());
            var sequenceBarrier = _ringBuffer.NewBarrier();
            _handler = new ValueAdditionEventHandler();
            _batchEventProcessor = new BatchEventProcessor<ValueEvent>(_ringBuffer, sequenceBarrier, _handler);
            _ringBuffer.AddGatingSequences(_batchEventProcessor.Sequence);
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        public int RequiredProcessorCount => 2;

        public long Run(Stopwatch stopwatch)
        {
            var signal = new ManualResetEvent(false);
            var expectedCount = _batchEventProcessor.Sequence.Value + _iterations * _batchSize;
            _handler.Reset(signal, expectedCount);
            _executor.Execute(_batchEventProcessor.Run);
            stopwatch.Start();

            var rb = _ringBuffer;
            for (var i = 0; i < _iterations; i++)
            {
                var hi = rb.Next(_batchSize);
                var lo = hi - (_batchSize - 1);
                for (var l = lo; l <= hi; l++)
                {
                    rb[l].Value = (i);
                }
                rb.Publish(lo, hi);
            }

            signal.WaitOne();
            stopwatch.Stop();
            PerfTestUtil.WaitForEventProcessorSequence(expectedCount, _batchEventProcessor);
            _batchEventProcessor.Halt();

            PerfTestUtil.FailIfNot(_expectedResult, _handler.Value, $"Handler should have processed {_expectedResult} events, but was: {_handler.Value}");

            return _batchSize * _iterations;
        }
    }
}