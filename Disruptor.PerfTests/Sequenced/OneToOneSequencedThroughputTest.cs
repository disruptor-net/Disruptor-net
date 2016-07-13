using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.PerfTests.Support;

namespace Disruptor.PerfTests.Sequenced
{
    /// <summary>
    /// 
    /// UniCast a series of items between 1 publisher and 1 event processor.
    ///
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
    /// | P1 |---\| RB |/---| SB |   | EP1 |
    /// +----+    +====+    +====+   +-----+
    ///      claim       get   ^        |
    ///                        |        |
    ///                        +--------+
    ///                          waitFor
    ///
    /// P1  - Publisher 1
    /// RB  - RingBuffer
    /// SB  - SequenceBarrier
    /// EP1 - EventProcessor 1
    /// </summary>
    public class OneToOneSequencedThroughputTest : IThroughputTest
    {
        private const int _bufferSize = 1024 * 64;
        private const long _iterations = 1000L * 1000L * 100L;

        private readonly RingBuffer<ValueEvent> _ringBuffer;
        private readonly ValueAdditionEventHandler _eventHandler;
        private readonly ManualResetEvent _latch;
        private readonly long _expectedResult = PerfTestUtil.AccumulatedAddition(_iterations);
        private readonly BatchEventProcessor<ValueEvent> _batchEventProcessor;
        private readonly IExecutor _executor = new BasicExecutor(TaskScheduler.Current);

        public OneToOneSequencedThroughputTest()
        {
            _latch = new ManualResetEvent(false);
            _eventHandler = new ValueAdditionEventHandler();
            _ringBuffer = RingBuffer<ValueEvent>.CreateSingleProducer(() => new ValueEvent(), _bufferSize, new YieldingWaitStrategy());
            var sequenceBarrier = _ringBuffer.NewBarrier();
            _batchEventProcessor = new BatchEventProcessor<ValueEvent>(_ringBuffer, sequenceBarrier, _eventHandler);
            _ringBuffer.AddGatingSequences(_batchEventProcessor.Sequence);
        }

        public long Run(Stopwatch stopwatch)
        {
            long expectedCount = _batchEventProcessor.Sequence.Value + _iterations;

            _latch.Reset();
            _eventHandler.Reset(_latch, expectedCount);
            _executor.Execute(_batchEventProcessor.Run);
            stopwatch.Start();

            for (long i = 0; i < _iterations; i++)
            {
                long sequence = _ringBuffer.Next();
                _ringBuffer[sequence].Value = i;
                _ringBuffer.Publish(sequence);
            }

            _latch.WaitOne();
            stopwatch.Stop();
            PerfTestUtil.WaitForEventProcessorSequence(expectedCount, _batchEventProcessor);
            _batchEventProcessor.Halt();

            PerfTestUtil.FailIfNot(_expectedResult, _eventHandler.Value, $"Handler should have processed {_expectedResult} events, but was: {_eventHandler.Value}");

            return _iterations;
        }

        public int RequiredProcessorCount => 2;
    }
}