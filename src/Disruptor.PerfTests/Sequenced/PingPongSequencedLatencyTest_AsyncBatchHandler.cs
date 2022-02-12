using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;
using HdrHistogram;

namespace Disruptor.PerfTests.Sequenced;

public class PingPongSequencedLatencyTest_AsyncBatchHandler : ILatencyTest
{
    private const int _bufferSize = 1024;
    private const long _iterations = 100 * 1000 * 30;
    private const long _pauseNanos = 1000;

    ///////////////////////////////////////////////////////////////////////////////////////////////

    private readonly RingBuffer<PingPongEvent> _pingBuffer;
    private readonly RingBuffer<PingPongEvent> _pongBuffer;

    private readonly IAsyncSequenceBarrier _pongBarrier;
    private readonly Pinger _pinger;
    private readonly IAsyncEventProcessor<PingPongEvent> _pingProcessor;

    private readonly IAsyncSequenceBarrier _pingBarrier;
    private readonly Ponger _ponger;
    private readonly IAsyncEventProcessor<PingPongEvent> _pongProcessor;

    public PingPongSequencedLatencyTest_AsyncBatchHandler()
    {
        _pingBuffer = RingBuffer<PingPongEvent>.CreateSingleProducer(() => new PingPongEvent(), _bufferSize, new AsyncWaitStrategy());
        _pongBuffer = RingBuffer<PingPongEvent>.CreateSingleProducer(() => new PingPongEvent(), _bufferSize, new AsyncWaitStrategy());

        _pingBarrier = (IAsyncSequenceBarrier)_pingBuffer.NewBarrier();
        _pongBarrier = (IAsyncSequenceBarrier)_pongBuffer.NewBarrier();

        _pinger = new Pinger(_pongBuffer, _pauseNanos);
        _ponger = new Ponger(_pingBuffer);

        _pingProcessor = EventProcessorFactory.Create(_pingBuffer, _pingBarrier, _pinger);
        _pongProcessor = EventProcessorFactory.Create(_pongBuffer, _pongBarrier, _ponger);

        _pingBuffer.AddGatingSequences(_pingProcessor.Sequence);
        _pongBuffer.AddGatingSequences(_pongProcessor.Sequence);
    }

    public int RequiredProcessorCount => 2;

    ///////////////////////////////////////////////////////////////////////////////////////////////

    public void Run(Stopwatch stopwatch, HistogramBase histogram)
    {
        var startCountdown = new CountdownEvent(3);
        var completedSignal = new ManualResetEvent(false);
        _pinger.Reset(startCountdown, completedSignal, histogram);
        _ponger.Reset(startCountdown);

        var processorTask1 = _pongProcessor.Start();
        var processorTask2 = _pingProcessor.Start();
        _pongProcessor.WaitUntilStarted(TimeSpan.FromSeconds(5));
        _pingProcessor.WaitUntilStarted(TimeSpan.FromSeconds(5));

        startCountdown.Signal();
        startCountdown.Wait();
        stopwatch.Start();

        completedSignal.WaitOne();

        stopwatch.Stop();

        _pingProcessor.Halt();
        _pongProcessor.Halt();
        Task.WaitAll(processorTask1, processorTask2);
    }

    private class PingPongEvent
    {
        public long Counter;
    }

    private class Pinger : IAsyncBatchEventHandler<PingPongEvent>
    {
        private readonly RingBuffer<PingPongEvent> _buffer;
        private readonly long _pauseTimeNs;
        private readonly long _pauseTimeTicks;
        private HistogramBase _histogram;
        private CountdownEvent _startCountdown;
        private ManualResetEvent _completedSignal;
        private long _t0;
        private long _counter;

        public Pinger(RingBuffer<PingPongEvent> buffer, long pauseTimeNs)
        {
            _buffer = buffer;
            _pauseTimeNs = pauseTimeNs;
            _pauseTimeTicks = LatencyTestSession.ConvertNanoToStopwatchTicks(pauseTimeNs);
        }

        public ValueTask OnBatch(EventBatch<PingPongEvent> batch, long sequence)
        {
            foreach (var data in batch)
            {
                var t1 = Stopwatch.GetTimestamp();
                _histogram.RecordValueWithExpectedInterval(LatencyTestSession.ConvertStopwatchTicksToNano(t1 - _t0), _pauseTimeNs);

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
