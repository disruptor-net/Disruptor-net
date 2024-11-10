using System.Diagnostics;
using System.Linq;
using System.Threading;
using Disruptor.Util;
using HdrHistogram;

namespace Disruptor.PerfTests.Latency.OneWay;

public class OneWayAwaitLatencyTest_QueueUserWorkItem : ILatencyTest, IExternalTest
{
    private const long _iterations = 1_000_000;
    private static readonly long _pause = StopwatchUtil.GetTimestampFromMicroseconds(10);

    private readonly ManualResetEventSlim _completed = new();
    private readonly Waiter[] _waiters;

    public OneWayAwaitLatencyTest_QueueUserWorkItem()
    {
        _waiters = Enumerable.Range(0, 100).Select(x => new Waiter(_completed)).ToArray();
    }

    public int RequiredProcessorCount => 2;

    public void Run(LatencySessionContext sessionContext)
    {
        _completed.Reset();

        foreach (var waiter in _waiters)
        {
            waiter.Initialize(sessionContext.Histogram);
        }

        var pause = _pause;
        var next = Stopwatch.GetTimestamp() + pause;

        sessionContext.Start();

        for (var i = 0; i < _iterations; i++)
        {
            var now = Stopwatch.GetTimestamp();
            while (now < next)
            {
                Thread.Yield();
                now = Stopwatch.GetTimestamp();
            }

            var waiter = _waiters[i % _waiters.Length];
            waiter.Notify(now);

            next = now + pause;
        }

        // Ensure last iteration was processed
        var lastWaiter = _waiters[(_iterations - 1) % _waiters.Length];
        lastWaiter.Notify(-1);

        _completed.Wait();

        sessionContext.Stop();
    }

    private class Waiter : IThreadPoolWorkItem
    {
        private readonly ManualResetEventSlim _completed;
        private HistogramBase _histogram;
        private long _startTimestamp;

        public Waiter(ManualResetEventSlim completed)
        {
            _completed = completed;
        }

        public void Initialize(HistogramBase histogram)
        {
            _histogram = histogram;
            _startTimestamp = 0;
        }

        public void Notify(long timestamp)
        {
            while (Volatile.Read(ref _startTimestamp) != 0)
            {
                Thread.Sleep(0);
            }

            Volatile.Write(ref _startTimestamp,  timestamp);
            ThreadPool.UnsafeQueueUserWorkItem(this, true);
        }

        public void Execute()
        {
            if (_startTimestamp == -1)
            {
                _completed.Set();
                return;
            }

            var timestamp = Stopwatch.GetTimestamp();
            var duration = timestamp - _startTimestamp;

            _histogram.RecordValue(StopwatchUtil.ToNanoseconds(duration));

            Volatile.Write(ref _startTimestamp,  0);
        }
    }
}
