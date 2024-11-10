using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Util;
using HdrHistogram;

namespace Disruptor.PerfTests.Latency.OneWay;

public class OneWayAwaitLatencyTest_TaskCompletionSource : ILatencyTest, IExternalTest
{
    private const long _iterations = 1_000_000;
    private static readonly long _pause = StopwatchUtil.GetTimestampFromMicroseconds(10);

    private readonly ManualResetEventSlim _completed = new();
    private readonly Waiter[] _waiters;

    public OneWayAwaitLatencyTest_TaskCompletionSource()
    {
        _waiters = Enumerable.Range(0, 100).Select(x => new Waiter()).ToArray();
    }

    public int RequiredProcessorCount => 2;

    public void Run(LatencySessionContext sessionContext)
    {
        _completed.Reset();

        foreach (var waiter in _waiters)
        {
            waiter.Initialize(sessionContext.Histogram);
            waiter.Start();
        }

        var pause = _pause;
        var next = Stopwatch.GetTimestamp() + pause;

        sessionContext.Start();

        for (int i = 0; i < _iterations; i++)
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

        foreach (var waiter in _waiters)
        {
            waiter.Notify(-1);
        }

        foreach (var waiter in _waiters)
        {
            waiter.Task.Wait();
        }

        sessionContext.Stop();
    }

    private class Waiter
    {
        private TaskCompletionSource<bool> _taskCompletionSource;
        private HistogramBase _histogram;
        private long _startTimestamp;

        public Task Task { get; private set; }

        public void Initialize(HistogramBase histogram)
        {
            _histogram = histogram;
            _taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref _startTimestamp, 0);
        }

        public void Notify(long timestamp)
        {
            while (Volatile.Read(ref _startTimestamp) != 0)
            {
                Thread.Sleep(0);
            }

            Volatile.Write(ref _startTimestamp, timestamp);
            _taskCompletionSource.SetResult(true);
        }

        public void Start()
        {
            Task = RunImpl();

            async Task RunImpl()
            {
                while (true)
                {
                    await _taskCompletionSource.Task.ConfigureAwait(false);

                    if (_startTimestamp == -1)
                        break;

                    var timestamp = Stopwatch.GetTimestamp();
                    var duration = timestamp - _startTimestamp;

                    _histogram.RecordValue(StopwatchUtil.ToNanoseconds(duration));

                    _taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    Volatile.Write(ref _startTimestamp, 0);
                }
            }
        }
    }
}
