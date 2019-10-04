using System;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace Disruptor.Benchmarks
{
    public class BatchEventProcessorBenchmarks
    {
        private readonly Sequence _sequence = new Sequence();
        private readonly RingBuffer<TestEvent> _ringBuffer;
        private readonly TestEventHandler _eventHandler;
        private readonly ISequenceBarrier _sequenceBarrier;

        public BatchEventProcessorBenchmarks()
        {
            _ringBuffer = new RingBuffer<TestEvent>(() => new TestEvent(), new SingleProducerSequencer(4096, new SpinWaitWaitStrategy()));
            _eventHandler = new TestEventHandler();
            _sequenceBarrier = _ringBuffer.NewBarrier();

            _ringBuffer.PublishEvent().Dispose();
        }

        public volatile int Running;

        [Benchmark]
        public long ProcessEvent()
        {
            var nextSequence = 0L;
            try
            {
                var availableSequence = _sequenceBarrier.WaitFor(nextSequence);

                while (nextSequence <= availableSequence)
                {
                    var evt = _ringBuffer[nextSequence];
                    _eventHandler.OnEvent(evt, nextSequence, nextSequence == availableSequence);
                    nextSequence++;
                }

                _sequence.SetValue(availableSequence);
            }
            catch (TimeoutException)
            {
                NotifyTimeout(_sequence.Value);
            }
            catch (AlertException)
            {
                if (Running != 2)
                {
                    return nextSequence;
                }
            }
            catch (Exception)
            {
                _sequence.SetValue(nextSequence);
                nextSequence++;
            }

            return nextSequence;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void NotifyTimeout(long sequenceValue)
        {
        }

        public class TestEvent
        {
            public long Data { get; set; }
        }

        public class TestEventHandler : IEventHandler<TestEvent>
        {
            public void OnEvent(TestEvent data, long sequence, bool endOfBatch)
            {
            }
        }
    }
}
