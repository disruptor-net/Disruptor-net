using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.PerfTests.Support;
using Disruptor.Processing;
using HdrHistogram;

namespace Disruptor.PerfTests.Sequenced
{
    public class PingPongSequencedLatencyTest_Value : ILatencyTest
    {
        private const int _bufferSize = 1024;
        private const long _iterations = 100 * 1000 * 30;
        private const long _pauseNanos = 1000;

        ///////////////////////////////////////////////////////////////////////////////////////////////

        private readonly ValueRingBuffer<PerfValueEvent> _pingBuffer;
        private readonly ValueRingBuffer<PerfValueEvent> _pongBuffer;

        private readonly ISequenceBarrier _pongBarrier;
        private readonly Pinger _pinger;
        private readonly IValueEventProcessor<PerfValueEvent> _pingProcessor;

        private readonly ISequenceBarrier _pingBarrier;
        private readonly Ponger _ponger;
        private readonly IValueEventProcessor<PerfValueEvent> _pongProcessor;

        public PingPongSequencedLatencyTest_Value()
        {
            _pingBuffer = ValueRingBuffer<PerfValueEvent>.CreateSingleProducer(PerfValueEvent.EventFactory, _bufferSize, new BlockingWaitStrategy());
            _pongBuffer = ValueRingBuffer<PerfValueEvent>.CreateSingleProducer(PerfValueEvent.EventFactory, _bufferSize, new BlockingWaitStrategy());

            _pingBarrier = _pingBuffer.NewBarrier();
            _pongBarrier = _pongBuffer.NewBarrier();

            _pinger = new Pinger(_pingBuffer, _iterations, _pauseNanos);
            _ponger = new Ponger(_pongBuffer);

            _pingProcessor = EventProcessorFactory.Create(_pongBuffer,_pongBarrier, _pinger);
            _pongProcessor = EventProcessorFactory.Create(_pingBuffer,_pingBarrier, _ponger);

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

            var processorTask1 = _pongProcessor.Start();
            var processorTask2 = _pingProcessor.Start();
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

        private class Pinger : IValueEventHandler<PerfValueEvent>
        {
            private readonly ValueRingBuffer<PerfValueEvent> _buffer;
            private readonly long _maxEvents;
            private readonly long _pauseTimeNs;
            private readonly long _pauseTimeTicks;
            private HistogramBase _histogram;
            private long _t0;
            private long _counter;
            private CountdownEvent _globalSignal;
            private ManualResetEvent _signal;

            public Pinger(ValueRingBuffer<PerfValueEvent> buffer, long maxEvents, long pauseTimeNs)
            {
                _buffer = buffer;
                _maxEvents = maxEvents;
                _pauseTimeNs = pauseTimeNs;
                _pauseTimeTicks = LatencyTestSession.ConvertNanoToStopwatchTicks(pauseTimeNs);
            }

            public void OnEvent(ref PerfValueEvent data, long sequence, bool endOfBatch)
            {
                var t1 = Stopwatch.GetTimestamp();

                _histogram.RecordValueWithExpectedInterval(LatencyTestSession.ConvertStopwatchTicksToNano(t1 - _t0), _pauseTimeNs);

                if (data.Value < _maxEvents)
                {
                    while (_pauseTimeTicks > (Stopwatch.GetTimestamp() - t1))
                    {
                        Thread.Yield();
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

                Thread.Sleep(1000);

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

        private class Ponger : IValueEventHandler<PerfValueEvent>
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
