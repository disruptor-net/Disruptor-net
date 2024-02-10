using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;
using Disruptor.Util;
using HdrHistogram;

namespace Disruptor.PerfTests.Latency.OneWay;

public class OneWaySequencedLatencyTest_Channel : ILatencyTest, IExternalTest, IDisposable
{
    private const int _bufferSize = 1024;
    private const long _iterations = 100 * 1000 * 30;
    private static readonly long _pause = StopwatchUtil.GetTimestampFromMicroseconds(10);

    private readonly Channel<PerfEvent> _channel;
    private readonly Consumer _consumer;

    public OneWaySequencedLatencyTest_Channel()
    {
        _channel = Channel.CreateBounded<PerfEvent>(new BoundedChannelOptions(_bufferSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
        _consumer = new Consumer(_channel);
    }

    public int RequiredProcessorCount => 2;

    public void Run(Stopwatch stopwatch, HistogramBase histogram)
    {
        _consumer.Initialize(histogram);

        var consumerTask = Task.Run(_consumer.Run);

        _consumer.Started.Wait();

        Thread.Sleep(1000);

        var pause = _pause;
        var next = Stopwatch.GetTimestamp() + pause;

        stopwatch.Start();

        for (int i = 0; i < _iterations; i++)
        {
            var now = Stopwatch.GetTimestamp();
            while (now < next)
            {
                Thread.Yield();
                now = Stopwatch.GetTimestamp();
            }

            while (!_channel.Writer.TryWrite(new PerfEvent { Value = now }))
            {
                Thread.Yield();
            }

            next = now + pause;
        }

        while (!_channel.Writer.TryWrite(new PerfEvent { Value = -1 }))
        {
            Thread.Yield();
        }

        _consumer.Completed.Wait();

        stopwatch.Stop();

        consumerTask.Wait();
    }

    public void Dispose()
    {
    }

    private class Consumer
    {
        private readonly Channel<PerfEvent> _channel;
        private HistogramBase _histogram;

        public Consumer(Channel<PerfEvent> channel)
        {
            _channel = channel;
        }

        public ManualResetEventSlim Started { get; } = new();
        public ManualResetEventSlim Completed { get; } = new();

        public void Initialize(HistogramBase histogram)
        {
            _histogram = histogram;
            Completed.Reset();
        }

        public async Task Run()
        {
            Started.Set();

            while (await _channel.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (_channel.Reader.TryRead(out var perfEvent))
                {
                    if (perfEvent.Value == -1)
                    {
                        Completed.Set();
                        return;
                    }

                    var handlerTimestamp = Stopwatch.GetTimestamp();
                    var duration = handlerTimestamp - perfEvent.Value;

                    _histogram.RecordValue(StopwatchUtil.ToNanoseconds(duration));
                }
            }
        }
    }
}
