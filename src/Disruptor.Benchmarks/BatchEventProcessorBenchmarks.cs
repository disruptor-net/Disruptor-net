using BenchmarkDotNet.Attributes;

namespace Disruptor.Benchmarks
{
    public class BatchEventProcessorBenchmarks
    {
        private readonly RingBuffer<TestEvent> _ringBuffer;
        private readonly BatchEventProcessor<TestEvent> _processor;
        private readonly TestEventHandler _eventHandler;

        public BatchEventProcessorBenchmarks()
        {
            _ringBuffer = new RingBuffer<TestEvent>(() => new TestEvent(), new SingleProducerSequencer(4096, new SpinWaitWaitStrategy()));
            _eventHandler = new TestEventHandler();
            _processor = new BatchEventProcessor<TestEvent>(_ringBuffer, _ringBuffer.NewBarrier(), _eventHandler);

            _eventHandler.Processor = _processor;
            _eventHandler.RingBuffer = _ringBuffer;
            _ringBuffer.AddGatingSequences(_processor.Sequence);
        }

        [IterationSetup]
        public void BeforeRun()
        {
            var sequence = _ringBuffer.Next(4096);
            for (var i = 0; i < 4096; i++)
            {
                _ringBuffer[sequence + i].Data = i;
            }
            _ringBuffer.Publish(sequence);
        }

        [Benchmark(OperationsPerInvoke = 4096)]
        public void Run()
        {
            _processor.Run();
        }

        public class TestEvent
        {
            public long Data { get; set; }
        }

        public class TestEventHandler : IEventHandler<TestEvent>
        {
            public long Sum { get; set; }
            public RingBuffer<TestEvent> RingBuffer { get; set; }
            public BatchEventProcessor<TestEvent> Processor { get; set; }

            public void OnEvent(TestEvent data, long sequence, bool endOfBatch)
            {
                Sum += data.Data;

                if (data.Data == 4095)
                    Processor.Halt();
                else
                    RingBuffer.Publish(sequence + 1);
            }
        }
    }
}
