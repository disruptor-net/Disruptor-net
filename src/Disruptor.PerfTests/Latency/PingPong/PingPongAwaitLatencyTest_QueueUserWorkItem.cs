using System;
using System.Diagnostics;
using System.Threading;
using Disruptor.Util;
using HdrHistogram;

namespace Disruptor.PerfTests.Latency.PingPong;

public class PingPongAwaitLatencyTest_QueueUserWorkItem : ILatencyTest, IExternalTest
{
    private const long _iterations = 1_000_000;
    private static readonly long _pause = StopwatchUtil.GetTimestampFromMicroseconds(10);

    private readonly Pinger _pinger;

    public PingPongAwaitLatencyTest_QueueUserWorkItem()
    {
        _pinger = new Pinger();
    }

    public void Run(Stopwatch stopwatch, HistogramBase histogram)
    {
        var signal = new ManualResetEvent(false);
        _pinger.Reset(signal, histogram);

        stopwatch.Start();
        _pinger.SendPing();

        signal.WaitOne();
        stopwatch.Stop();

#if AWAIT_LATENCY_THREAD_COUNT
        Console.WriteLine($"THREAD COUNT: {Ponger.ThreadCount}");
#endif
    }

    public int RequiredProcessorCount => 2;

    private class Pinger : IThreadPoolWorkItem
    {
        private readonly Ponger _ponger;

        private HistogramBase _histogram;
        private ManualResetEvent _signal;
        private long _counter;
        private long _t0;

        public Pinger()
        {
            _ponger = new Ponger(this);
        }

#if AWAIT_LATENCY_THREAD_COUNT
        public int ThreadId { get; private set; }
#endif

        public void Reset(ManualResetEvent signal, HistogramBase histogram)
        {
            _signal = signal;
            _histogram = histogram;
            _counter = 0;
        }

        public void SendPing()
        {
            if (_counter == _iterations)
            {
                _signal.Set();
                return;
            }

#if AWAIT_LATENCY_THREAD_COUNT
            ThreadId = Thread.CurrentThread.ManagedThreadId;
#endif

            _counter++;
            _t0 = Stopwatch.GetTimestamp();

            ThreadPool.UnsafeQueueUserWorkItem(_ponger, true);
        }

        public void Execute()
        {
            var t1 = Stopwatch.GetTimestamp();

            _histogram.RecordValue(StopwatchUtil.ToNanoseconds(t1 - _t0));

            while (Stopwatch.GetTimestamp() - t1 < _pause)
            {
                Thread.Sleep(0);
            }

            SendPing();
        }
    }

    private class Ponger : IThreadPoolWorkItem
    {
        private readonly Pinger _pinger;

        public Ponger(Pinger pinger)
        {
            _pinger = pinger;
        }

        public void Execute()
        {
#if AWAIT_LATENCY_THREAD_COUNT
            if (_pinger.ThreadId != Thread.CurrentThread.ManagedThreadId)
            {
                ThreadCount++;
            }
#endif

            ThreadPool.UnsafeQueueUserWorkItem(_pinger, true);
        }

#if AWAIT_LATENCY_THREAD_COUNT
        public static int ThreadCount;
#endif
    }
}
