﻿using System;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Disruptor.Processing;

namespace Disruptor.Benchmarks.Processing;

/// <summary>
/// Runs processors on a filled ring buffer then exits.
/// The events will be processing as a single batch.
/// </summary>
public class EventProcessorBenchmarks_ProcessFilledRingBuffer
{
    private const int _ringBufferSize = 131072;

    private readonly RingBuffer<XEvent> _ringBuffer;
    private IEventProcessor<XEvent> _processor;
    private IEventProcessor<XEvent> _batchProcessor;

    public EventProcessorBenchmarks_ProcessFilledRingBuffer()
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
        _processor = EventProcessorFactory.Create(_ringBuffer, _ringBuffer.NewBarrier(), new XEventHandler(() => _processor.Halt()));
        _batchProcessor = EventProcessorFactory.Create(_ringBuffer, _ringBuffer.NewBarrier(), new XBatchEventHandler(() => _batchProcessor.Halt()));
    }

    // [Benchmark(OperationsPerInvoke = _ringBufferSize)]
    // public void Run()
    // {
    //     _processor.Run();
    // }
    //
    // [Benchmark(OperationsPerInvoke = _ringBufferSize)]
    // public void RunBach()
    // {
    //     _batchProcessor.Run();
    // }

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

        public long Sum { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void OnEvent(XEvent data, long sequence, bool endOfBatch)
        {
            Sum += data.Data;

            if (sequence + 1 == _ringBufferSize)
                _shutdown.Invoke();
        }
    }

    public class XBatchEventHandler : IBatchEventHandler<XEvent>
    {
        private readonly Action _shutdown;

        public XBatchEventHandler(Action shutdown)
        {
            _shutdown = shutdown;
        }

        public long Sum { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void OnBatch(EventBatch<XEvent> batch, long sequence)
        {
            foreach (var data in batch)
            {
                Sum += data.Data;
            }

            if (sequence + batch.Length == _ringBufferSize)
                _shutdown.Invoke();
        }
    }
}
