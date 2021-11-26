using System;
using BenchmarkDotNet.Attributes;
using Disruptor.Benchmarks.Reference;
using Disruptor.Processing;

namespace Disruptor.Benchmarks
{
    public class BatchEventProcessorBenchmarks
    {
        private const int _ringBufferSize = 131072;

        private readonly RingBuffer<XEvent> _ringBuffer;
        private IBatchEventProcessor<XEvent> _processor;
        private IBatchEventProcessor<XEvent> _processorRef;

        public BatchEventProcessorBenchmarks()
        {
            _ringBuffer = new RingBuffer<XEvent>(() => new XEvent(), new SingleProducerSequencer(_ringBufferSize, new BusySpinWaitStrategy()));

            for (var i = 0; i < _ringBufferSize; i++)
            {
                using var scope = _ringBuffer.PublishEvent();
                scope.Event().Data = i;
            }
        }

        [IterationSetup]
        public void Setup()
        {
            _processor = BatchEventProcessorFactory.Create(_ringBuffer, _ringBuffer.NewBarrier(), new XEventHandler(() => _processor.Halt()));
            _processorRef = BatchEventProcessorFactory.Create(_ringBuffer, _ringBuffer.NewBarrier(), new XEventHandler(() => _processorRef.Halt()), typeof(BatchEventProcessorRef<,,,,>));
        }

        [Benchmark(OperationsPerInvoke = _ringBufferSize)]
        public void Run()
        {
            _processor.Run();
        }

        [Benchmark(OperationsPerInvoke = _ringBufferSize)]
        public void RunRef()
        {
            _processorRef.Run();
        }

        public class XEvent
        {
            public long Data { get; set; }
        }

        public class XEventHandler : IEventHandler<XEvent>
        {
            private readonly Action _shutdown;

            public XEventHandler(Action shutdown)
            {
                _shutdown = shutdown;
            }

            public void OnEvent(XEvent data, long sequence, bool endOfBatch)
            {
                if (sequence == _ringBufferSize - 1)
                    _shutdown.Invoke();
            }
        }
    }
}
