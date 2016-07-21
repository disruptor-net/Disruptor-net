using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.PerfTests.Support;
using HdrHistogram;

namespace Disruptor.PerfTests.Queue
{
    public class PingPingQueueLatencyTest : ILatencyTest, IQueueTest
    {
        private const int _bufferSize = 1024;
        private const long _iterations = 1000 * 1000 * 30;
        private const long _pauseDurationInNano = 1000;
        private static readonly IExecutor _executor = new BasicExecutor(TaskScheduler.Current);

        private readonly BlockingCollection<long> _pingQueue = new BlockingCollection<long>(new LockFreeBoundedQueue<long>(_bufferSize), _bufferSize);
        private readonly BlockingCollection<long> _pongQueue = new BlockingCollection<long>(new LockFreeBoundedQueue<long>(_bufferSize), _bufferSize);
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

            _executor.Execute(_pinger.Run);
            _executor.Execute(_ponger.Run);

            globalSignal.Signal();
            globalSignal.Wait();
            stopwatch.Start();
            signal.WaitOne();
            stopwatch.Stop();

            cancellationToken.Cancel();
        }

        public int RequiredProcessorCount { get; } = 2;

        private class QueuePinger
        {
            private readonly BlockingCollection<long> _pingQueue;
            private readonly BlockingCollection<long> _pongQueue;
            private readonly long _maxEvents;
            private readonly long _pauseTimeInNano;
            private readonly double _pauseDurationInStopwatchTicks;

            private HistogramBase _histogram;
            private ManualResetEvent _signal;
            private CountdownEvent _globalSignal;
            private long _counter;

            public QueuePinger(BlockingCollection<long> pingQueue, BlockingCollection<long> pongQueue, long maxEvents, long pauseTimeInNano)
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
                long response = -1;

                while (response < _maxEvents)
                {
                    var t0 = Stopwatch.GetTimestamp();
                    while (!_pingQueue.TryAdd(_counter++))
                        Thread.Yield();
                    response = _pongQueue.Take();
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
                _counter = 0;
            }
        }

        public class QueuePonger
        {
            private readonly BlockingCollection<long> _pingQueue;
            private readonly BlockingCollection<long> _pongQueue;
            private CancellationToken _cancellationToken;
            private CountdownEvent _globalSignal;

            public QueuePonger(BlockingCollection<long> pingQueue, BlockingCollection<long> pongQueue)
            {
                _pingQueue = pingQueue;
                _pongQueue = pongQueue;
            }

            public void Run()
            {
                _globalSignal.Signal();
                _globalSignal.Wait();

                try
                {
                    while (!_cancellationToken.IsCancellationRequested)
                    {
                        var value = _pingQueue.Take(_cancellationToken);
                        while (!_pongQueue.TryAdd(value))
                            Thread.Yield();
                    }
                }
                catch (OperationCanceledException) { }
            }

            public void Reset(CountdownEvent globalSignal, CancellationToken cancellationToken)
            {
                _globalSignal = globalSignal;
                _cancellationToken = cancellationToken;
            }
        }
    }
}