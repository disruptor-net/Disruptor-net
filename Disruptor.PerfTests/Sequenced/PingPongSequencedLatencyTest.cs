﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.PerfTests.Support;
using HdrHistogram;

namespace Disruptor.PerfTests.Sequenced
{
    public class PingPongSequencedLatencyTest : ILatencyTest
    {
        private static readonly IExecutor _executor = new BasicExecutor(TaskScheduler.Current);
        private const int _bufferSize = 1024;
        private const long _iterations = 1000 * 1000 * 30;
        private const int _pauseDurationInNanos = 1000;

        ///////////////////////////////////////////////////////////////////////////////////////////////

        private readonly RingBuffer<ValueEvent> _pingBuffer;
        private readonly RingBuffer<ValueEvent> _pongBuffer;

        private readonly ISequenceBarrier _pongBarrier;
        private readonly Pinger _pinger;
        private readonly BatchEventProcessor<ValueEvent> _pingProcessor;

        private readonly ISequenceBarrier _pingBarrier;
        private readonly Ponger _ponger;
        private readonly BatchEventProcessor<ValueEvent> _pongProcessor;

        public PingPongSequencedLatencyTest()
        {
            _pingBuffer = RingBuffer<ValueEvent>.CreateSingleProducer(() => new ValueEvent(), _bufferSize, new YieldingWaitStrategy());
            _pongBuffer = RingBuffer<ValueEvent>.CreateSingleProducer(() => new ValueEvent(), _bufferSize, new YieldingWaitStrategy());

            _pingBarrier = _pingBuffer.NewBarrier();
            _pongBarrier = _pongBuffer.NewBarrier();

            _pinger = new Pinger(_pingBuffer, _iterations, _pauseDurationInNanos);
            _ponger = new Ponger(_pongBuffer);

            _pingProcessor = new BatchEventProcessor<ValueEvent>(_pongBuffer,_pongBarrier, _pinger);
            _pongProcessor = new BatchEventProcessor<ValueEvent>(_pingBuffer,_pingBarrier, _ponger);

            _pingBuffer.AddGatingSequences(_pongProcessor.Sequence);
            _pongBuffer.AddGatingSequences(_pingProcessor.Sequence);
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        public void Run(Stopwatch stopwatch, HistogramBase histogram)
        {
            var globalSignal = new CountdownEvent(3);
            var signal = new ManualResetEvent(false);
            _pinger.Reset(globalSignal, signal, histogram);
            _ponger.Reset(globalSignal);

            var processorTask1 = _executor.Execute(_pongProcessor.Run);
            var processorTask2 = _executor.Execute(_pingProcessor.Run);

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

        public int RequiredProcessorCount { get; } = 2;

        private class Pinger : IEventHandler<ValueEvent>, ILifecycleAware
        {
            private readonly RingBuffer<ValueEvent> _buffer;
            private readonly long _maxEvents;
            private readonly int _pauseDurationInNanos;
            private double _pauseDurationInStopwatchTicks;
            private HistogramBase _histogram;
            private long _t0;
            private long _counter;
            private CountdownEvent _globalSignal;
            private ManualResetEvent _signal;

            public Pinger(RingBuffer<ValueEvent> buffer, long maxEvents, int pauseDurationInNanos)
            {
                _buffer = buffer;
                _maxEvents = maxEvents;

                _pauseDurationInNanos = pauseDurationInNanos;
                _pauseDurationInStopwatchTicks = LatencyTestSession.ConvertNanoToStopwatchTicks(pauseDurationInNanos);
            }

            public void OnEvent(ValueEvent data, long sequence, bool endOfBatch)
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

        private class Ponger : IEventHandler<ValueEvent>, ILifecycleAware
        {
            private readonly RingBuffer<ValueEvent> _buffer;
            private CountdownEvent _globalSignal;

            public Ponger(RingBuffer<ValueEvent> buffer)
            {
                _buffer = buffer;
            }

            public void OnEvent(ValueEvent data, long sequence, bool endOfBatch)
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