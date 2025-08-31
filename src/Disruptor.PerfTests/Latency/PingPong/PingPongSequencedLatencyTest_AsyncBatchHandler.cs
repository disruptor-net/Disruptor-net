using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;
using Disruptor.Processing;
using Disruptor.Util;
using HdrHistogram;

namespace Disruptor.PerfTests.Latency.PingPong;

public class PingPongSequencedLatencyTest_AsyncBatchHandler : ILatencyTest
{
    private const int _bufferSize = 1024;
    private const long _iterations = 100 * 1000 * 30;
    private const long _pauseNanos = 1000;

    ///////////////////////////////////////////////////////////////////////////////////////////////

    private readonly Pinger _pinger;
    private readonly IAsyncEventProcessor<PingPongEvent> _pingProcessor;

    private readonly Ponger _ponger;
    private readonly IAsyncEventProcessor<PingPongEvent> _pongProcessor;

    public PingPongSequencedLatencyTest_AsyncBatchHandler()
    {
        var pingBuffer = RingBuffer<PingPongEvent>.CreateSingleProducer(() => new PingPongEvent(), _bufferSize, new AsyncWaitStrategy());
        var pongBuffer = RingBuffer<PingPongEvent>.CreateSingleProducer(() => new PingPongEvent(), _bufferSize, new AsyncWaitStrategy());

        var pingBarrier = pingBuffer.NewAsyncBarrier();
        var pongBarrier = pongBuffer.NewAsyncBarrier();

        _pinger = new Pinger(pongBuffer);
        _ponger = new Ponger(pingBuffer);

        _pingProcessor = EventProcessorFactory.Create(pingBuffer, pingBarrier, _pinger);
        _pongProcessor = EventProcessorFactory.Create(pongBuffer, pongBarrier, _ponger);

        pingBuffer.AddGatingSequences(_pingProcessor.Sequence);
        pongBuffer.AddGatingSequences(_pongProcessor.Sequence);
    }

    public int RequiredProcessorCount => 2;

    ///////////////////////////////////////////////////////////////////////////////////////////////

    public void Run(LatencySessionContext sessionContext)
    {
        var startCountdown = new CountdownEvent(3);
        var completedSignal = new ManualResetEvent(false);
        _pinger.Reset(startCountdown, completedSignal, sessionContext.Histogram);
        _ponger.Reset(startCountdown);

        var startTask1 = _pongProcessor.Start();
        var startTask2 = _pingProcessor.Start();
        startTask1.Wait(TimeSpan.FromSeconds(5));
        startTask2.Wait(TimeSpan.FromSeconds(5));

        startCountdown.Signal();
        startCountdown.Wait();
        sessionContext.Start();

        completedSignal.WaitOne();

        sessionContext.Stop();

        var stopTask1 = _pingProcessor.Halt();
        var stopTask2 = _pongProcessor.Halt();
        Task.WaitAll(stopTask1, stopTask2);

        PerfTestUtil.FailIf(_pinger.HasInvalidValue, "Pinger processed an invalid value");
    }

    private class PingPongEvent
    {
        public long Counter;
    }

    private class Pinger : IAsyncBatchEventHandler<PingPongEvent>
    {
        private readonly RingBuffer<PingPongEvent> _buffer;
        private readonly long _pauseTimeTicks;
        private HistogramBase _histogram;
        private CountdownEvent _startCountdown;
        private ManualResetEvent _completedSignal;
        private long _t0;
        private long _counter;
        private long _expectedCounter;

        public Pinger(RingBuffer<PingPongEvent> buffer)
        {
            _buffer = buffer;
            _pauseTimeTicks = StopwatchUtil.GetTimestampFromNanoseconds(_pauseNanos);
        }

        public bool HasInvalidValue { get; private set; }

        public ValueTask OnBatch(EventBatch<PingPongEvent> batch, long sequence)
        {
            foreach (var data in batch)
            {
                var t1 = Stopwatch.GetTimestamp();

                HasInvalidValue |= data.Counter != _expectedCounter;
                _histogram.RecordValueWithExpectedInterval(StopwatchUtil.ToNanoseconds(t1 - _t0), _pauseNanos);

                if (data.Counter == _iterations)
                {
                    _completedSignal.Set();
                }
                else
                {
                    while (_pauseTimeTicks > Stopwatch.GetTimestamp() - t1)
                    {
                        Thread.Yield();
                    }

                    SendPing();
                }
            }

            return ValueTask.CompletedTask;
        }

        private void SendPing()
        {
            _expectedCounter = _counter;
            _t0 = Stopwatch.GetTimestamp();
            var next = _buffer.Next();
            _buffer[next].Counter = _counter;
            _buffer.Publish(next);

            _counter++;
        }

        public void OnStart()
        {
            _startCountdown.Signal();
            _startCountdown.Wait();

            Thread.Sleep(1000);

            SendPing();
        }

        public void OnShutdown()
        {
        }

        public void Reset(CountdownEvent startCountdown, ManualResetEvent completedSignal, HistogramBase histogram)
        {
            _startCountdown = startCountdown;
            _completedSignal = completedSignal;
            _histogram = histogram;
            _counter = 0;
            _expectedCounter = 0;
            HasInvalidValue = false;
        }
    }

    private class Ponger : IAsyncBatchEventHandler<PingPongEvent>
    {
        private readonly RingBuffer<PingPongEvent> _buffer;
        private CountdownEvent _startCountdown;

        public Ponger(RingBuffer<PingPongEvent> buffer)
        {
            _buffer = buffer;
        }

        public ValueTask OnBatch(EventBatch<PingPongEvent> batch, long sequence)
        {
            foreach (var data in batch)
            {
                var next = _buffer.Next();
                var pingEvent = _buffer[next];
                pingEvent.Counter = data.Counter;
                _buffer.Publish(next);
            }

            return ValueTask.CompletedTask;
        }

        public void OnStart()
        {
            _startCountdown.Signal();
            _startCountdown.Wait();
        }

        public void OnShutdown()
        {
        }

        public void Reset(CountdownEvent startCountdown)
        {
            _startCountdown = startCountdown;
        }
    }
}
