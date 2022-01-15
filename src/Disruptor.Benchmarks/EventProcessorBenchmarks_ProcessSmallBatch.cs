using System;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Disruptor.Processing;

namespace Disruptor.Benchmarks
{
    /// <summary>
    /// Use partial copies of the processor types to benchmark the processing of a few events.
    /// </summary>
    public class EventProcessorBenchmarks_ProcessSmallBatch
    {
        private const int _ringBufferSize = 32;

        private readonly PartialEventProcessor<XEvent, RingBuffer<XEvent>, XEventHandler> _processor;
        private readonly PartialBatchEventProcessor<XEvent, RingBuffer<XEvent>, XBatchEventHandler> _batchProcessor;

        public EventProcessorBenchmarks_ProcessSmallBatch()
        {
            var ringBuffer = new RingBuffer<XEvent>(() => new XEvent(), new SingleProducerSequencer(_ringBufferSize, new BusySpinWaitStrategy()));

            for (var i = 0; i < _ringBufferSize; i++)
            {
                using var scope = ringBuffer.PublishEvent();
                scope.Event().Data = i;
            }

            _processor = new PartialEventProcessor<XEvent, RingBuffer<XEvent>, XEventHandler>(ringBuffer, new XEventHandler());
            _batchProcessor = new PartialBatchEventProcessor<XEvent, RingBuffer<XEvent>, XBatchEventHandler>(ringBuffer, new XBatchEventHandler());
        }

        [Params(1, 2, 5, 10)]
        public int BatchSize { get; set; }

        [Benchmark]
        public void Process()
        {
            _processor.Process(BatchSize, 1);
        }

        [Benchmark]
        public void ProcessBatch()
        {
            _batchProcessor.Process(BatchSize, 1);
        }

        public class XEvent
        {
            public long Data { get; set; }
        }

        /// <summary>
        /// Partial copy of <see cref="EventProcessor{T, TDataProvider, TSequenceBarrier, TEventHandler}"/>
        /// </summary>
        public class PartialEventProcessor<T, TDataProvider, TEventHandler>
            where T : class
            where TDataProvider : IDataProvider<T>
            where TEventHandler : IEventHandler<T>
        {
            private readonly Sequence _sequence = new();
            private TDataProvider _dataProvider;
            private TEventHandler _eventHandler;

            public PartialEventProcessor(TDataProvider dataProvider, TEventHandler eventHandler)
            {
                _dataProvider = dataProvider;
                _eventHandler = eventHandler;
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void Process(long availableSequence, long nextSequence)
            {
                _eventHandler.OnBatchStart(availableSequence - nextSequence + 1);

                while (nextSequence <= availableSequence)
                {
                    var evt = _dataProvider[nextSequence];
                    _eventHandler.OnEvent(evt, nextSequence, nextSequence == availableSequence);
                    nextSequence++;
                }

                _sequence.SetValue(availableSequence);
            }
        }

        public class XEventHandler : IEventHandler<XEvent>
        {
            public long Sum { get; set; }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void OnEvent(XEvent data, long sequence, bool endOfBatch)
            {
                Sum += data.Data;
            }
        }

        /// <summary>
        /// Partial copy of <see cref="BatchEventProcessor{T, TDataProvider, TSequenceBarrier, TEventHandler}"/>
        /// </summary>
        public class PartialBatchEventProcessor<T, TDataProvider, TEventHandler>
            where T : class
            where TDataProvider : IDataProvider<T>
            where TEventHandler : IBatchEventHandler<T>
        {
            private readonly Sequence _sequence = new();
            private TDataProvider _dataProvider;
            private TEventHandler _eventHandler;

            public PartialBatchEventProcessor(TDataProvider dataProvider, TEventHandler eventHandler)
            {
                _dataProvider = dataProvider;
                _eventHandler = eventHandler;
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void Process(long availableSequence, long nextSequence)
            {
                if (availableSequence >= nextSequence)
                {
                    var batch = _dataProvider.GetBatch(nextSequence, availableSequence);
                    _eventHandler.OnBatch(batch, nextSequence);
                    nextSequence += batch.Length;
                }

                _sequence.SetValue(nextSequence - 1);
            }
        }

        public class XBatchEventHandler : IBatchEventHandler<XEvent>
        {
            public long Sum { get; set; }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void OnBatch(EventBatch<XEvent> batch, long sequence)
            {
                foreach (var data in batch)
                {
                    Sum += data.Data;
                }
            }
        }
    }
}
