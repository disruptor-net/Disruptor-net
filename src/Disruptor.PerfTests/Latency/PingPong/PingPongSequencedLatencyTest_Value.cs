using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;
using Disruptor.Processing;
using Disruptor.Util;
using HdrHistogram;

namespace Disruptor.PerfTests.Latency.PingPong;

public class PingPongSequencedLatencyTest_Value : ILatencyTest
{
    private const int _bufferSize = 1024;
    private const long _iterations = 100 * 1000 * 30;
    private const long _pauseNanos = 1000;

    ///////////////////////////////////////////////////////////////////////////////////////////////

    private readonly Pinger _pinger;
    private readonly IValueEventProcessor<PerfValueEvent> _pingProcessor;

    private readonly Ponger _ponger;
    private readonly IValueEventProcessor<PerfValueEvent> _pongProcessor;

    public PingPongSequencedLatencyTest_Value(ProgramOptions options)
    {
        var pingBuffer = ValueRingBuffer<PerfValueEvent>.CreateSingleProducer(PerfValueEvent.EventFactory, _bufferSize, options.GetWaitStrategy());
        var pongBuffer = ValueRingBuffer<PerfValueEvent>.CreateSingleProducer(PerfValueEvent.EventFactory, _bufferSize, options.GetWaitStrategy());

        var pingBarrier = pingBuffer.NewBarrier();
        var pongBarrier = pongBuffer.NewBarrier();

        _pinger = new Pinger(pingBuffer, options.GetCustomCpu(0));
        _ponger = new Ponger(pongBuffer, options.GetCustomCpu(1));

        _pingProcessor = EventProcessorFactory.Create(pongBuffer,pongBarrier, _pinger);
        _pongProcessor = EventProcessorFactory.Create(pingBuffer,pingBarrier, _ponger);

        pingBuffer.AddGatingSequences(_pongProcessor.Sequence);
        pongBuffer.AddGatingSequences(_pingProcessor.Sequence);
    }

    public int RequiredProcessorCount => 2;

    ///////////////////////////////////////////////////////////////////////////////////////////////

    public void Run(LatencySessionContext sessionContext)
    {
        var globalSignal = new CountdownEvent(3);
        var signal = new ManualResetEvent(false);
        _pinger.Reset(globalSignal, signal, sessionContext.Histogram);
        _ponger.Reset(globalSignal);

        var startTask1 = _pongProcessor.Start();
        var startTask2 = _pingProcessor.Start();
        startTask1.Wait(TimeSpan.FromSeconds(5));
        startTask2.Wait(TimeSpan.FromSeconds(5));

        globalSignal.Signal();
        globalSignal.Wait();
        sessionContext.Start();
        // running here
        signal.WaitOne();
        sessionContext.Stop();

        var shutdownTask1 = _pingProcessor.Halt();
        var shutdownTask2 = _pongProcessor.Halt();
        Task.WaitAll(shutdownTask1, shutdownTask2);

        PerfTestUtil.FailIf(_pinger.HasInvalidValue, "Pinger processed an invalid value");
    }

    private class Pinger : IValueEventHandler<PerfValueEvent>
    {
        private readonly ValueRingBuffer<PerfValueEvent> _buffer;
        private readonly int? _cpu;
        private readonly long _pauseTimeTicks;
        private HistogramBase _histogram;
        private long _t0;
        private long _counter;
        private long _expectedValue;
        private CountdownEvent _globalSignal;
        private ManualResetEvent _signal;
        private ThreadAffinityScope _affinityScope;

        public Pinger(ValueRingBuffer<PerfValueEvent> buffer, int? cpu)
        {
            _buffer = buffer;
            _cpu = cpu;
            _pauseTimeTicks = StopwatchUtil.GetTimestampFromNanoseconds(_pauseNanos);
        }

        public bool HasInvalidValue { get; private set; }

        public void OnEvent(ref PerfValueEvent data, long sequence, bool endOfBatch)
        {
            var t1 = Stopwatch.GetTimestamp();

            HasInvalidValue |= data.Value != _expectedValue;
            _histogram.RecordValueWithExpectedInterval(StopwatchUtil.ToNanoseconds(t1 - _t0), _pauseNanos);

            if (data.Value < _iterations)
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
            _expectedValue = _counter;
            _t0 = Stopwatch.GetTimestamp();
            var next = _buffer.Next();
            _buffer[next].Value = _counter;
            _buffer.Publish(next);

            _counter++;
        }

        public void OnStart()
        {
            _affinityScope = ThreadAffinityUtil.SetThreadAffinity(_cpu, ThreadPriority.Highest);

            _globalSignal.Signal();
            _globalSignal.Wait();

            Thread.Sleep(1000);

            Send();
        }

        public void OnShutdown()
        {
            _affinityScope.Dispose();
        }

        public void Reset(CountdownEvent globalSignal, ManualResetEvent signal, HistogramBase histogram)
        {
            _histogram = histogram;
            _globalSignal = globalSignal;
            _signal = signal;

            _counter = 0;
            _expectedValue = 0;
            HasInvalidValue = false;
        }
    }

    private class Ponger : IValueEventHandler<PerfValueEvent>
    {
        private readonly ValueRingBuffer<PerfValueEvent> _buffer;
        private readonly int? _cpu;
        private CountdownEvent _globalSignal;
        private ThreadAffinityScope _affinityScope;

        public Ponger(ValueRingBuffer<PerfValueEvent> buffer, int? cpu)
        {
            _buffer = buffer;
            _cpu = cpu;
        }

        public void OnEvent(ref PerfValueEvent data, long sequence, bool endOfBatch)
        {
            var next = _buffer.Next();
            _buffer[next].Value = data.Value;
            _buffer.Publish(next);
        }

        public void OnStart()
        {
            _affinityScope = ThreadAffinityUtil.SetThreadAffinity(_cpu, ThreadPriority.Highest);

            _globalSignal.Signal();
            _globalSignal.Wait();
        }

        public void OnShutdown()
        {
            _affinityScope.Dispose();
        }

        public void Reset(CountdownEvent globalSignal)
        {
            _globalSignal = globalSignal;
        }
    }
}
