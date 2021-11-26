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

        private readonly PartialEventProcessor<XEvent, RingBuffer<XEvent>, XEventHandler, EventProcessorFactory.NoopBatchStartAware> _processor;
#if NETCOREAPP
        private readonly PartialBatchEventProcessor<XEvent, RingBuffer<XEvent>, XBatchEventHandler> _batchProcessor;
#endif

        public EventProcessorBenchmarks_ProcessSmallBatch()
        {
            var ringBuffer = new RingBuffer<XEvent>(() => new XEvent(), new SingleProducerSequencer(_ringBufferSize, new BusySpinWaitStrategy()));

            for (var i = 0; i < _ringBufferSize; i++)
            {
                using var scope = ringBuffer.PublishEvent();
                scope.Event().Data = i;
            }

            _processor = new PartialEventProcessor<XEvent, RingBuffer<XEvent>, XEventHandler, EventProcessorFactory.NoopBatchStartAware>(ringBuffer, new XEventHandler(), new EventProcessorFactory.NoopBatchStartAware());
#if NETCOREAPP
            _batchProcessor = new PartialBatchEventProcessor<XEvent, RingBuffer<XEvent>, XBatchEventHandler>(ringBuffer, new XBatchEventHandler());
#endif
        }

        [Params(1, 2, 5, 10)]
        public int BatchSize { get; set; }

        [Benchmark]
        public void Process()
        {
            _processor.Process(BatchSize, 1);
        }

#if NETCOREAPP
        [Benchmark]
        public void ProcessBatch()
        {
            _batchProcessor.Process(BatchSize, 1);
        }
#endif

        public class XEvent
        {
            public long Data { get; set; }
        }

        /// <summary>
        /// Partial copy of <see cref="EventProcessor{T, TDataProvider, TSequenceBarrier, TEventHandler, TBatchStartAware}"/>
        /// </summary>
        public class PartialEventProcessor<T, TDataProvider, TEventHandler, TBatchStartAware>
            where T : class
            where TDataProvider : IDataProvider<T>
            where TEventHandler : IEventHandler<T>
            where TBatchStartAware : IBatchStartAware
        {
            private readonly Sequence _sequence = new Sequence();
            private TDataProvider _dataProvider;
            private TEventHandler _eventHandler;
            private TBatchStartAware _batchStartAware;

            public PartialEventProcessor(TDataProvider dataProvider, TEventHandler eventHandler, TBatchStartAware batchStartAware)
            {
                _dataProvider = dataProvider;
                _eventHandler = eventHandler;
                _batchStartAware = batchStartAware;
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void Process(long availableSequence, long nextSequence)
            {
                _batchStartAware.OnBatchStart(availableSequence - nextSequence + 1);

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

#if NETCOREAPP
        /// <summary>
        /// Partial copy of <see cref="BatchEventProcessor{T, TDataProvider, TSequenceBarrier, TEventHandler}"/>
        /// </summary>
        public class PartialBatchEventProcessor<T, TDataProvider, TEventHandler>
            where T : class
            where TDataProvider : IDataProvider<T>
            where TEventHandler : IBatchEventHandler<T>
        {
            private readonly Sequence _sequence = new Sequence();
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
                    var span = _dataProvider[nextSequence, availableSequence];
                    _eventHandler.OnBatch(span, nextSequence);
                    nextSequence += span.Length;
                }

                _sequence.SetValue(nextSequence - 1);
            }
        }

        public class XBatchEventHandler : IBatchEventHandler<XEvent>
        {
            public long Sum { get; set; }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void OnBatch(ReadOnlySpan<XEvent> batch, long sequence)
            {
                foreach (var data in batch)
                {
                    Sum += data.Data;
                }
            }
        }
#endif
    }
}
