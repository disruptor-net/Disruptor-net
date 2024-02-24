using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Util;
using HdrHistogram;

namespace Disruptor.PerfTests.Latency.PingPong;

public class PingPongAwaitLatencyTest_TaskCompletionSource : ILatencyTest, IExternalTest
{
    private const long _iterations = 1_000_000;
    private static readonly long _pause = StopwatchUtil.GetTimestampFromMicroseconds(10);

    private readonly Pinger _pinger;

    public PingPongAwaitLatencyTest_TaskCompletionSource()
    {
        _pinger = new Pinger();
    }

    public void Run(Stopwatch stopwatch, HistogramBase histogram)
    {
        _pinger.Reset(histogram);

        stopwatch.Start();

        var task = _pinger.Start();
        task.Wait();

        stopwatch.Stop();
    }

    public int RequiredProcessorCount => 2;

    private class Pinger
    {
        private readonly Ponger _ponger;

        private HistogramBase _histogram;
        private TaskCompletionSource<bool> _taskCompletionSource;

        public Pinger()
        {
            _ponger = new Ponger(this);
            _taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public void Reset(HistogramBase histogram)
        {
            _histogram = histogram;
        }

        public Task Start()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            return Task.WhenAll(_ponger.Run(cancellationTokenSource.Token), Run(cancellationTokenSource));
        }

        private async Task Run(CancellationTokenSource cancellationTokenSource)
        {
            for (var i = 0; i < _iterations; i++)
            {
                var t0 = Stopwatch.GetTimestamp();

                _ponger.Notify();

                await _taskCompletionSource.Task.ConfigureAwait(false);

                _taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

                var t1 = Stopwatch.GetTimestamp();

                _histogram.RecordValue(StopwatchUtil.ToNanoseconds(t1 - t0));

                while (Stopwatch.GetTimestamp() - t1 < _pause)
                {
                    Thread.Sleep(0);
                }
            }

            cancellationTokenSource.Cancel();

            _ponger.Notify();
        }

        public void Notify()
        {
            _taskCompletionSource.SetResult(true);
        }
    }

    private class Ponger
    {
        private readonly Pinger _pinger;
        private TaskCompletionSource<bool> _taskCompletionSource;

        public Ponger(Pinger pinger)
        {
            _pinger = pinger;
            _taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public void Notify()
        {
            _taskCompletionSource.SetResult(true);
        }

        public async Task Run(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _taskCompletionSource.Task.ConfigureAwait(false);

                _taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _pinger.Notify();
            }
        }
    }
}
