using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.PerfTests.Support;

namespace Disruptor.PerfTests.Translator
{
    /// <summary>
    ///     UniCast a series of items between 1 publisher and 1 event processor using the EventTranslator API
    ///     +----+    +-----+
    ///     | P1 |--->| EP1 |
    ///     +----+    +-----+
    ///     Disruptor:
    ///     ==========
    ///     track to prevent wrap
    ///     +------------------+
    ///     |                  |
    ///     |                  v
    ///     +----+    +====+    +====+   +-----+
    ///     | P1 |--->| RB |/---| SB |   | EP1 |
    ///     +----+    +====+    +====+   +-----+
    ///     claim      get    ^        |
    ///     |        |
    ///     +--------+
    ///     waitFor
    ///     P1  - Publisher 1
    ///     RB  - RingBuffer
    ///     SB  - SequenceBarrier
    ///     EP1 - EventProcessor 1
    /// </summary>
    public class OneToOneTranslatorThroughputTest : IThroughputTest
    {
        private const int _bufferSize = 1024 * 64;
        private const long _iterations = 1000L * 1000L * 100L;
        private readonly long _expectedResult = PerfTestUtil.AccumulatedAddition(_iterations);
        private readonly ValueAdditionEventHandler _handler = new ValueAdditionEventHandler();
        private readonly RingBuffer<ValueEvent> _ringBuffer;
        private readonly MutableLong _value = new MutableLong();

        public OneToOneTranslatorThroughputTest()
        {
            var disruptor = new Disruptor<ValueEvent>(ValueEvent.EventFactory,
                                                      _bufferSize, 
                                                      TaskScheduler.Default,
                                                      ProducerType.Single,
                                                      new YieldingWaitStrategy());
            disruptor.HandleEventsWith(_handler);
            _ringBuffer = disruptor.Start();
        }

        public int RequiredProcessorCount => 2;

        public long Run(ThroughputSessionContext sessionContext)
        {
            var value = _value;

            var latch = new ManualResetEvent(false);
            var expectedCount = _ringBuffer.GetMinimumGatingSequence() + _iterations;

            _handler.Reset(latch, expectedCount);
            sessionContext.Start();

            var rb = _ringBuffer;

            for (long l = 0; l < _iterations; l++)
            {
                value.Value = l;
                rb.PublishEvent(Translator.Instance, value);
            }

            latch.WaitOne();
            sessionContext.Stop();
            WaitForEventProcessorSequence(expectedCount);

            PerfTestUtil.FailIfNot(_expectedResult, _handler.Value);

            sessionContext.SetBatchData(_handler.BatchesProcessedCount, _iterations);

            return _iterations;
        }

        private void WaitForEventProcessorSequence(long expectedCount)
        {
            while (_ringBuffer.GetMinimumGatingSequence() != expectedCount)
            {
                Thread.Sleep(1);
            }
        }

        private class Translator : IEventTranslatorOneArg<ValueEvent, MutableLong>
        {
            public static readonly Translator Instance = new Translator();

            public void TranslateTo(ValueEvent @event, long sequence, MutableLong arg0)
            {
                @event.Value = arg0.Value;
            }
        }

        private class MutableLong
        {
            public long Value { get; set; }
        }
    }
}
