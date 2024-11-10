using System;
using System.Diagnostics;
using System.Threading;
using Disruptor.Dsl;
using Disruptor.PerfTests.Support;
using Disruptor.Util;
using HdrHistogram;

namespace Disruptor.PerfTests.Latency.OneWay;

public class OneWaySequencedLatencyTest : ILatencyTest, IDisposable
{
    private const int _bufferSize = 1024;
    private const long _iterations = 100 * 1000 * 30;
    private static readonly long _pause = StopwatchUtil.GetTimestampFromMicroseconds(10);

    private readonly Disruptor<PerfEvent> _disruptor;
    private readonly Handler _handler;

    public OneWaySequencedLatencyTest()
    {
        _disruptor = new Disruptor<PerfEvent>(() => new PerfEvent(), _bufferSize, new BlockingWaitStrategy());
        _handler = new Handler();
        _disruptor.HandleEventsWith(_handler);
        _disruptor.Start();
    }

    public int RequiredProcessorCount => 2;

    public void Run(LatencySessionContext sessionContext)
    {
        _handler.Initialize(sessionContext.Histogram);
        _handler.Started.Wait();

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

    private class Handler : IEventHandler<PerfEvent>
    {
        private HistogramBase _histogram;

        public ManualResetEventSlim Started { get; } = new();
        public ManualResetEventSlim Completed { get; } = new();

        public void OnStart()
        {
            Started.Set();
        }

        public void Initialize(HistogramBase histogram)
        {
            _histogram = histogram;
            Completed.Reset();
        }

        public void OnEvent(PerfEvent data, long sequence, bool endOfBatch)
        {
            if (data.Value == -1)
            {
                Completed.Set();
                return;
            }

            var handlerTimestamp = Stopwatch.GetTimestamp();
            var duration = handlerTimestamp - data.Value;

            _histogram.RecordValue(StopwatchUtil.ToNanoseconds(duration));
        }
    }
}
