using System;
using System.Diagnostics;
using System.Threading;
using Disruptor.Dsl;
using Disruptor.PerfTests.Support;
using Disruptor.Util;
using HdrHistogram;

namespace Disruptor.PerfTests.Latency.OneWay;

public class OneWaySequencedLatencyTest_BatchHandler : ILatencyTest, IDisposable
{
    private const int _bufferSize = 1024;
    private const long _iterations = 100 * 1000 * 30;
    private static readonly long _pause = StopwatchUtil.GetTimestampFromMicroseconds(10);

    private readonly ProgramOptions _options;
    private readonly Disruptor<PerfEvent> _disruptor;
    private readonly Handler _handler;

    public OneWaySequencedLatencyTest_BatchHandler(ProgramOptions options)
    {
        _options = options;
        _disruptor = new Disruptor<PerfEvent>(() => new PerfEvent(), _bufferSize, options.GetWaitStrategy());
        _handler = new Handler(options.GetCustomCpu(1));
        _disruptor.HandleEventsWith(_handler);
        _disruptor.Start();
    }

    public int RequiredProcessorCount => 2;

    public void Run(LatencySessionContext sessionContext)
    {
        _handler.Initialize(sessionContext.Histogram);
        _handler.Started.Wait();

        using var _ = ThreadAffinityUtil.SetThreadAffinity(_options.GetCustomCpu(0), ThreadPriority.Highest);

        var pause = _pause;
        var next = Stopwatch.GetTimestamp() + pause;

        sessionContext.Start();

        var ringBuffer = _disruptor.RingBuffer;

        for (int i = 0; i < _iterations; i++)
        {
            var now = Stopwatch.GetTimestamp();
            while (now < next)
            {
                Thread.Yield();
                now = Stopwatch.GetTimestamp();
            }

            var s = ringBuffer.Next();
            ringBuffer[s].Value = now;
            ringBuffer.Publish(s);

            next = now + pause;
        }

        var lastS = ringBuffer.Next();
        ringBuffer[lastS].Value = -1;
        ringBuffer.Publish(lastS);

        _handler.Completed.Wait();

        sessionContext.Stop();
    }

    public void Dispose()
    {
        _disruptor.Shutdown();
    }

    private class Handler : IBatchEventHandler<PerfEvent>
    {
        private readonly int? _cpu;
        private HistogramBase _histogram;
        private ThreadAffinityScope _affinityScope;

        public Handler(int? cpu)
        {
            _cpu = cpu;
        }

        public ManualResetEventSlim Started { get; } = new();
        public ManualResetEventSlim Completed { get; } = new();

        public void OnStart()
        {
            _affinityScope = ThreadAffinityUtil.SetThreadAffinity(_cpu, ThreadPriority.Highest);
            Started.Set();
        }

        public void OnShutdown()
        {
            _affinityScope.Dispose();
        }

        public void Initialize(HistogramBase histogram)
        {
            _histogram = histogram;
            Completed.Reset();
        }

        public void OnBatch(EventBatch<PerfEvent> batch, long sequence)
        {
            foreach (var data in batch.AsSpan())
            {
                if (data.Value == -1)
                {
                    Completed.Set();
                    continue;
                }

                var handlerTimestamp = Stopwatch.GetTimestamp();
                var duration = handlerTimestamp - data.Value;

                _histogram.RecordValue(StopwatchUtil.ToNanoseconds(duration));
            }
        }
    }
}
