using System;
using BenchmarkDotNet.Attributes;
using Disruptor.Processing;

namespace Disruptor.Benchmarks;

public class ValueEventProcessorBenchmarks
{
    private const int _ringBufferSize = 131072;

    private readonly ValueRingBuffer<XEvent> _ringBuffer;
    private IValueEventProcessor<XEvent> _processor;

    public ValueEventProcessorBenchmarks()
    {
        _ringBuffer = new ValueRingBuffer<XEvent>(() => new XEvent(), new SingleProducerSequencer(_ringBufferSize, new BusySpinWaitStrategy()));

        for (var i = 0; i < _ringBufferSize; i++)
        {
            using var scope = _ringBuffer.PublishEvent();
            scope.Event().Data = i;
        }
    }

    [IterationSetup]
    public void Setup()
    {
        _processor = EventProcessorFactory.Create(_ringBuffer, _ringBuffer.NewBarrier(), new XEventHandler(() => _processor.Halt()));
    }

    // [Benchmark(OperationsPerInvoke = _ringBufferSize)]
    // public void Run()
    // {
    //     _processor.Run();
    // }

    public struct XEvent
    {
        public long Data { get; set; }
    }

    public class XEventHandler : IValueEventHandler<XEvent>
    {
        private readonly Action _shutdown;

        public XEventHandler(Action shutdown)
        {
            _shutdown = shutdown;
        }

        public void OnEvent(ref XEvent data, long sequence, bool endOfBatch)
        {
            if (sequence == _ringBufferSize - 1)
                _shutdown.Invoke();
        }
    }
}
