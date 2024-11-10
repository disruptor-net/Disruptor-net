using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Disruptor.Util;
using HdrHistogram;

namespace Disruptor.PerfTests.Latency.PingPong;

public class PingPongAwaitLatencyTest_ManualResetValueTaskSourceCore : ILatencyTest, IExternalTest
{
    private const long _iterations = 1_000_000;
    private static readonly long _pause = StopwatchUtil.GetTimestampFromMicroseconds(10);

    private readonly Pinger _pinger;

    public PingPongAwaitLatencyTest_ManualResetValueTaskSourceCore()
    {
        _pinger = new Pinger();
    }

    public void Run(LatencySessionContext sessionContext)
    {
        _pinger.Reset(sessionContext.Histogram);

        sessionContext.Start();

        var task = _pinger.Start();
        task.Wait();

        sessionContext.Stop();
    }

    public int RequiredProcessorCount => 2;

    private class ValueTaskSource : IValueTaskSource<bool>
    {
        private ManualResetValueTaskSourceCore<bool> _valueTaskSourceCore = new() { RunContinuationsAsynchronously = true };

        protected void ResetTask()
        {
            _valueTaskSourceCore.Reset();
        }

        protected ValueTask<bool> GetTask()
        {
            return new ValueTask<bool>(this, _valueTaskSourceCore.Version);
        }

        protected void CompleteTask()
        {
            _valueTaskSourceCore.SetResult(true);
        }

        public bool GetResult(short token)
        {
            return _valueTaskSourceCore.GetResult(token);
        }

        public ValueTaskSourceStatus GetStatus(short token)
        {
            return _valueTaskSourceCore.GetStatus(token);
        }

        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            _valueTaskSourceCore.OnCompleted(continuation, state, token, flags);
        }
    }

    private class Pinger : ValueTaskSource
    {
        private readonly Ponger _ponger;

        private HistogramBase _histogram;

        public Pinger()
        {
            _ponger = new Ponger(this);
        }

        public void Reset(HistogramBase histogram)
        {
            _histogram = histogram;
            ResetTask();
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

                await GetTask().ConfigureAwait(false);

                ResetTask();

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
            CompleteTask();
        }
    }

    private class Ponger : ValueTaskSource
    {
        private readonly Pinger _pinger;

        public Ponger(Pinger pinger)
        {
            _pinger = pinger;
        }

        public void Notify()
        {
            CompleteTask();
        }

        public async Task Run(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await GetTask().ConfigureAwait(false);

                ResetTask();

                _pinger.Notify();
            }
        }
    }
}
