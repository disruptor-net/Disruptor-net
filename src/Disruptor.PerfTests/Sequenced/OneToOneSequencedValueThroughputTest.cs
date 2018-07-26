using System;
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
    public class OneToOneSequencedValueThroughputTest : IThroughputTest
    {
        private const int _bufferSize = 1024 * 64;
        private const long _iterations = 1000L * 1000L * 100L;

        private readonly ValueRingBuffer<PerfValueEvent> _ringBuffer;
        private readonly AdditionEventHandler _eventHandler;
        private readonly ManualResetEvent _latch;
        private readonly long _expectedResult = PerfTestUtil.AccumulatedAddition(_iterations);
        private readonly IValueBatchEventProcessor<PerfValueEvent> _batchEventProcessor;
        private readonly IExecutor _executor = new BasicExecutor(TaskScheduler.Current);

        public OneToOneSequencedValueThroughputTest()
        {
            _latch = new ManualResetEvent(false);
            _eventHandler = new AdditionEventHandler();
            _ringBuffer = ValueRingBuffer<PerfValueEvent>.CreateSingleProducer(PerfValueEvent.EventFactory, _bufferSize, new YieldingWaitStrategy());
            var sequenceBarrier = _ringBuffer.NewBarrier();
            _batchEventProcessor = BatchEventProcessorFactory.Create(_ringBuffer, sequenceBarrier, _eventHandler);
            _ringBuffer.AddGatingSequences(_batchEventProcessor.Sequence);
        }

        public int RequiredProcessorCount => 2;

        public long Run(ThroughputSessionContext sessionContext)
        {
            long expectedCount = _batchEventProcessor.Sequence.Value + _iterations;

            _latch.Reset();
            _eventHandler.Reset(_latch, expectedCount);
            var processorTask = _executor.Execute(_batchEventProcessor.Run);
            _batchEventProcessor.WaitUntilStarted(TimeSpan.FromSeconds(5));

            sessionContext.Start();

            for (long i = 0; i < _iterations; i++)
            {
                long sequence = _ringBuffer.Next();
                _ringBuffer[sequence].Value = i;
                _ringBuffer.Publish(sequence);
            }

            _latch.WaitOne();
            sessionContext.Stop();
            PerfTestUtil.WaitForEventProcessorSequence(expectedCount, _batchEventProcessor);
            _batchEventProcessor.Halt();
            processorTask.Wait(2000);

            sessionContext.SetBatchData(_eventHandler.BatchesProcessedCount.Value, _iterations);

            PerfTestUtil.FailIfNot(_expectedResult, _eventHandler.Value, $"Handler should have processed {_expectedResult} events, but was: {_eventHandler.Value}");

            return _iterations;
        }
    }
}
