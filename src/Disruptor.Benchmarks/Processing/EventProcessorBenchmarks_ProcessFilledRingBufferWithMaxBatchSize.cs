using System;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Disruptor.Processing;

namespace Disruptor.Benchmarks.Processing;

/// <summary>
/// Runs processors on a filled ring buffer then exits.
/// </summary>
public class EventProcessorBenchmarks_ProcessFilledRingBufferWithMaxBatchSize
{
    private const int _ringBufferSize = 131072;
    private const int _maxBatchSize = 50;

    private readonly RingBuffer<XEvent> _ringBuffer;
    private IEventProcessor<XEvent> _processorRef1;
    private IEventProcessor<XEvent> _processor;
    private XEventHandler _eventHandlerRef1;
    private XEventHandler _eventHandler;

    public EventProcessorBenchmarks_ProcessFilledRingBufferWithMaxBatchSize()
    {
        _ringBuffer = new RingBuffer<XEvent>(() => new XEvent(), new SingleProducerSequencer(_ringBufferSize, new BusySpinWaitStrategy()));

        for (var i = 0; i < _ringBufferSize; i++)
        {
            using var scope = _ringBuffer.PublishEvent();
            scope.Event().Data = 1;
        }

        _eventHandlerRef1 = new XEventHandler(() => _processorRef1.Halt());
        _processorRef1 = EventProcessorRef1.Create(_ringBuffer, _ringBuffer.NewBarrier(), _eventHandlerRef1);
        _eventHandler = new XEventHandler(() => _processor.Halt());
        _processor = EventProcessorFactory.Create(_ringBuffer, _ringBuffer.NewBarrier(), _eventHandler);
    }

    [Benchmark(OperationsPerInvoke = _ringBufferSize / _maxBatchSize)]
    public void RunRef2()
    {
        _processorRef1.Run();
        _eventHandlerRef1.SequenceCallback.SetValue(-1); // Reset processor
    }

    [Benchmark(OperationsPerInvoke = _ringBufferSize / _maxBatchSize)]
    public void Run()
    {
        _processor.Run();
        _eventHandler.SequenceCallback.SetValue(-1); // Reset processor
    }

    public class XEvent
    {
        public long Data { get; set; }
    }

    public class XEventHandler : IEventHandler<XEvent>, IEventProcessorSequenceAware
    {
        private readonly Action _shutdown;

        public XEventHandler(Action shutdown)
        {
            _shutdown = shutdown;
        }

        public long Sum { get; set; }

        public int? MaxBatchSize => _maxBatchSize;

        // [MethodImpl(MethodImplOptions.NoInlining)]
        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnEvent(XEvent data, long sequence, bool endOfBatch)
        {
            Sum += data.Data;

            if (sequence + 1 == _ringBufferSize)
                _shutdown.Invoke();
        }

        public Sequence SequenceCallback { get; private set; }

        public void SetSequenceCallback(Sequence sequenceCallback)
        {
            SequenceCallback = sequenceCallback;
        }
    }
}
