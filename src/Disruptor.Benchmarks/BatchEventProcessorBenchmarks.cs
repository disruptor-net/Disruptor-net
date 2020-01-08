using System;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Disruptor.Internal;

namespace Disruptor.Benchmarks
{
    public class BatchEventProcessorBenchmarks
    {
        private readonly IRunner _runner;

        public BatchEventProcessorBenchmarks()
        {
            var ringBuffer = new RingBuffer<XEvent>(() => new XEvent(), new SingleProducerSequencer(4096, new SpinWaitWaitStrategy()));
            var eventHandler = new XEventHandler();
            var sequenceBarrier = ringBuffer.NewBarrier();

            ringBuffer.PublishEvent().Dispose();

            var dataProviderProxy = StructProxy.CreateProxyInstance<IDataProvider<XEvent>>(ringBuffer);
            var sequenceBarrierProxy = StructProxy.CreateProxyInstance(sequenceBarrier);
            var eventHandlerProxy = StructProxy.CreateProxyInstance<IEventHandler<XEvent>>(eventHandler);
            var batchStartAwareProxy = new NoopBatchStartAware();

            var runnerType = typeof(Runner<,,,,>).MakeGenericType(typeof(XEvent), dataProviderProxy.GetType(), sequenceBarrierProxy.GetType(), eventHandlerProxy.GetType(), batchStartAwareProxy.GetType());
            _runner = (IRunner)Activator.CreateInstance(runnerType, dataProviderProxy, sequenceBarrierProxy, eventHandlerProxy, batchStartAwareProxy);
        }

        [Benchmark]
        public long ProcessEvent()
        {
            return _runner.ProcessEvent();
        }

        public class XEvent
        {
            public long Data { get; set; }
        }

        public class XEventHandler : IEventHandler<XEvent>
        {
            public long Sum;

            public void OnEvent(XEvent data, long sequence, bool endOfBatch)
            {
                Sum += data.Data;
            }
        }

        private struct NoopBatchStartAware : IBatchStartAware
        {
            public void OnBatchStart(long batchSize)
            {
            }
        }

        public interface IRunner
        {
            long ProcessEvent();
        }

        public class Runner<T, TDataProvider, TSequenceBarrier, TEventHandler, TBatchStartAware> : IRunner
            where T : class
            where TDataProvider : IDataProvider<T>
            where TSequenceBarrier : ISequenceBarrier
            where TEventHandler : IEventHandler<T>
            where TBatchStartAware : IBatchStartAware
        {
            private readonly Sequence _sequence = new Sequence();
            private IExceptionHandler<T> _exceptionHandler = new FatalExceptionHandler();

            private TDataProvider _dataProvider;
            private TSequenceBarrier _sequenceBarrier;
            private TEventHandler _eventHandler;
            private TBatchStartAware _batchStartAware;

            public volatile int Running;

            public Runner(TDataProvider dataProvider, TSequenceBarrier sequenceBarrier, TEventHandler eventHandler, TBatchStartAware batchStartAware)
            {
                _dataProvider = dataProvider;
                _sequenceBarrier = sequenceBarrier;
                _eventHandler = eventHandler;
                _batchStartAware = batchStartAware;
            }

            public long ProcessEvent()
            {
                T evt = null;
                var nextSequence = _sequence.Value + 1L;

                try
                {
                    var availableSequence = _sequenceBarrier.WaitFor(nextSequence);

                    _batchStartAware.OnBatchStart(availableSequence - nextSequence + 1);

                    while (nextSequence <= availableSequence)
                    {
                        evt = _dataProvider[nextSequence];
                        _eventHandler.OnEvent(evt, nextSequence, nextSequence == availableSequence);
                        nextSequence++;
                    }

                    //_sequence.SetValue(availableSequence);
                }
                catch (TimeoutException)
                {
                    NotifyTimeout(_sequence.Value);
                }
                catch (AlertException)
                {
                }
                catch (Exception ex)
                {
                    _exceptionHandler.HandleEventException(ex, nextSequence, evt);
                    _sequence.SetValue(nextSequence);
                    nextSequence++;
                }

                return nextSequence;
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private void NotifyTimeout(long sequence)
            {
                Console.WriteLine(sequence);
            }
        }
    }
}
