using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.PerfTests.Support;
using HdrHistogram;

namespace Disruptor.PerfTests.Sequenced
{
    public class PingPongSequencedValueLatencyTest : ILatencyTest
    {
        private static readonly IExecutor _executor = new BasicExecutor(TaskScheduler.Current);
        private const int _bufferSize = 1024;
        private const long _iterations = 100 * 1000 * 30;
        private const int _pauseDurationInNanos = 1000;

        ///////////////////////////////////////////////////////////////////////////////////////////////

        private readonly ValueRingBuffer<PerfValueEvent> _pingBuffer;
        private readonly ValueRingBuffer<PerfValueEvent> _pongBuffer;

        private readonly ISequenceBarrier _pongBarrier;
        private readonly Pinger _pinger;
        private readonly IValueBatchEventProcessor<PerfValueEvent> _pingProcessor;

        private readonly ISequenceBarrier _pingBarrier;
        private readonly Ponger _ponger;
        private readonly IValueBatchEventProcessor<PerfValueEvent> _pongProcessor;

        public PingPongSequencedValueLatencyTest()
        {
            _pingBuffer = ValueRingBuffer<PerfValueEvent>.CreateSingleProducer(PerfValueEvent.EventFactory, _bufferSize, new BlockingWaitStrategy());
            _pongBuffer = ValueRingBuffer<PerfValueEvent>.CreateSingleProducer(PerfValueEvent.EventFactory, _bufferSize, new BlockingWaitStrategy());

            _pingBarrier = _pingBuffer.NewBarrier();
            _pongBarrier = _pongBuffer.NewBarrier();

            _pinger = new Pinger(_pingBuffer, _iterations, _pauseDurationInNanos);
            _ponger = new Ponger(_pongBuffer);

            _pingProcessor = BatchEventProcessorFactory.Create(_pongBuffer,_pongBarrier, _pinger);
            _pongProcessor = BatchEventProcessorFactory.Create(_pingBuffer,_pingBarrier, _ponger);

            _pingBuffer.AddGatingSequences(_pongProcessor.Sequence);
            _pongBuffer.AddGatingSequences(_pingProcessor.Sequence);
        }

        public int RequiredProcessorCount => 2;

        ///////////////////////////////////////////////////////////////////////////////////////////////

        public void Run(Stopwatch stopwatch, HistogramBase histogram)
        {
            var globalSignal = new CountdownEvent(3);
            var signal = new ManualResetEvent(false);
            _pinger.Reset(globalSignal, signal, histogram);
            _ponger.Reset(globalSignal);

            var processorTask1 = _executor.Execute(_pongProcessor.Run);
            var processorTask2 = _executor.Execute(_pingProcessor.Run);
            _pongProcessor.WaitUntilStarted(TimeSpan.FromSeconds(5));
            _pingProcessor.WaitUntilStarted(TimeSpan.FromSeconds(5));

            globalSignal.Signal();
            globalSignal.Wait();
            stopwatch.Start();
            // running here
            signal.WaitOne();
            stopwatch.Stop();

            _pingProcessor.Halt();
            _pongProcessor.Halt();
            Task.WaitAll(processorTask1, processorTask2);
        }

        private class Pinger : IValueEventHandler<PerfValueEvent>, ILifecycleAware
        {
            private readonly ValueRingBuffer<PerfValueEvent> _buffer;
            private readonly long _maxEvents;
            private readonly int _pauseDurationInNanos;
            private double _pauseDurationInStopwatchTicks;
            private HistogramBase _histogram;
            private long _t0;
            private long _counter;
            private CountdownEvent _globalSignal;
            private ManualResetEvent _signal;

            public Pinger(ValueRingBuffer<PerfValueEvent> buffer, long maxEvents, int pauseDurationInNanos)
            {
                _buffer = buffer;
                _maxEvents = maxEvents;

                _pauseDurationInNanos = pauseDurationInNanos;
                _pauseDurationInStopwatchTicks = LatencyTestSession.ConvertNanoToStopwatchTicks(pauseDurationInNanos);
            }

            public void OnEvent(ref PerfValueEvent data, long sequence, bool endOfBatch)
            {
                var t1 = Stopwatch.GetTimestamp();

                _histogram.RecordValueWithExpectedInterval(LatencyTestSession.ConvertStopwatchTicksToNano(t1 - _t0), _pauseDurationInNanos);

                if (data.Value < _maxEvents)
                {
                    while (_pauseDurationInStopwatchTicks > (Stopwatch.GetTimestamp() - t1))
                    {
                        Thread.Sleep(0);
                    }

                    Send();
                }
                else
                {
                    _signal.Set();
                }
            }

            private void Send()
            {
                _t0 = Stopwatch.GetTimestamp();
                var next = _buffer.Next();
                _buffer[next].Value = _counter;
                _buffer.Publish(next);

                _counter++;
            }

            public void OnStart()
            {
                _globalSignal.Signal();
                _globalSignal.Wait();

                Send();
            }

            public void OnShutdown()
            {
            }

            public void Reset(CountdownEvent globalSignal, ManualResetEvent signal, HistogramBase histogram)
            {
                _histogram = histogram;
                _globalSignal = globalSignal;
                _signal = signal;

                _counter = 0;
            }
        }

        private class Ponger : IValueEventHandler<PerfValueEvent>, ILifecycleAware
        {
            private readonly ValueRingBuffer<PerfValueEvent> _buffer;
            private CountdownEvent _globalSignal;

            public Ponger(ValueRingBuffer<PerfValueEvent> buffer)
            {
                _buffer = buffer;
            }

            public void OnEvent(ref PerfValueEvent data, long sequence, bool endOfBatch)
            {
                var next = _buffer.Next();
                _buffer[next].Value = data.Value;
                _buffer.Publish(next);
            }

            public void OnStart()
            {
                _globalSignal.Signal();
                _globalSignal.Wait();
            }

            public void OnShutdown()
            {
            }

            public void Reset(CountdownEvent globalSignal)
            {
                _globalSignal = globalSignal;
            }
        }
    }
}
