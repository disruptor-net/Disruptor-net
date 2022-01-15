using System.Diagnostics;
using System.Threading;
using Disruptor.PerfTests.Support;
using HdrHistogram;

namespace Disruptor.PerfTests.External
{
    public class PingPingQueueLatencyTest : ILatencyTest, IExternalTest
    {
        private const int _bufferSize = 1024;
        private const long _iterations = 100 * 1000 * 30;
        private const long _pauseDurationInNano = 1000;

        private readonly ArrayConcurrentQueue<long> _pingQueue = new(_bufferSize);
        private readonly ArrayConcurrentQueue<long> _pongQueue = new(_bufferSize);
        private readonly QueuePinger _pinger;
        private readonly QueuePonger _ponger;

        public PingPingQueueLatencyTest()
        {
            _pinger = new QueuePinger(_pingQueue, _pongQueue, _iterations, _pauseDurationInNano);
            _ponger = new QueuePonger(_pingQueue, _pongQueue);
        }

        public void Run(Stopwatch stopwatch, HistogramBase histogram)
        {
            var cancellationToken = new CancellationTokenSource();
            var signal = new ManualResetEvent(false);
            var globalSignal = new CountdownEvent(3);
            _pinger.Reset(globalSignal, signal, histogram);
            _ponger.Reset(globalSignal, cancellationToken.Token);

            _pinger.Start();
            _ponger.Start();

            globalSignal.Signal();
            globalSignal.Wait();
            stopwatch.Start();
            signal.WaitOne();
            stopwatch.Stop();

            cancellationToken.Cancel();
        }

        public int RequiredProcessorCount => 2;

        private class QueuePinger
        {
            private readonly ArrayConcurrentQueue<long> _pingQueue;
            private readonly ArrayConcurrentQueue<long> _pongQueue;
            private readonly long _maxEvents;
            private readonly long _pauseTimeInNano;
            private readonly double _pauseDurationInStopwatchTicks;

            private HistogramBase _histogram;
            private ManualResetEvent _signal;
            private CountdownEvent _globalSignal;

            public QueuePinger(ArrayConcurrentQueue<long> pingQueue, ArrayConcurrentQueue<long> pongQueue, long maxEvents, long pauseTimeInNano)
            {
                _pingQueue = pingQueue;
                _pongQueue = pongQueue;
                _maxEvents = maxEvents;
                _pauseTimeInNano = pauseTimeInNano;
                _pauseDurationInStopwatchTicks = LatencyTestSession.ConvertNanoToStopwatchTicks(pauseTimeInNano);
            }

            public void Run()
            {
                _globalSignal.Signal();
                _globalSignal.Wait();

                Thread.Sleep(1000);
                long counter = 0;

                while (counter < _maxEvents)
                {
                    var t0 = Stopwatch.GetTimestamp();
                    _pingQueue.Enqueue(1L);
                    counter += _pongQueue.Dequeue();
                    var t1 = Stopwatch.GetTimestamp();

                    _histogram.RecordValueWithExpectedInterval(LatencyTestSession.ConvertStopwatchTicksToNano(t1 - t0), _pauseTimeInNano);

                    while (_pauseDurationInStopwatchTicks > (Stopwatch.GetTimestamp() - t1))
                    {
                        Thread.Sleep(0);
                    }
                }

                _signal.Set();
            }

            public void Reset(CountdownEvent globalSignal, ManualResetEvent signal, HistogramBase histogram)
            {
                _globalSignal = globalSignal;
                _signal = signal;
                _histogram = histogram;
            }

            public void Start()
            {
                PerfTestUtil.StartLongRunning(Run);
            }
        }

        public class QueuePonger
        {
            private readonly ArrayConcurrentQueue<long> _pingQueue;
            private readonly ArrayConcurrentQueue<long> _pongQueue;
            private CancellationToken _cancellationToken;
            private CountdownEvent _globalSignal;

            public QueuePonger(ArrayConcurrentQueue<long> pingQueue, ArrayConcurrentQueue<long> pongQueue)
            {
                _pingQueue = pingQueue;
                _pongQueue = pongQueue;
            }

            public void Run()
            {
                _globalSignal.Signal();
                _globalSignal.Wait();

                while (!_cancellationToken.IsCancellationRequested)
                {
                    long value;
                    if (_pingQueue.TryDequeue(out value))
                        _pongQueue.Enqueue(value);
                    else
                        Thread.Yield();
                }
            }

            public void Reset(CountdownEvent globalSignal, CancellationToken cancellationToken)
            {
                _globalSignal = globalSignal;
                _cancellationToken = cancellationToken;
            }

            public void Start()
            {
                PerfTestUtil.StartLongRunning(Run);
            }
        }
    }
}
