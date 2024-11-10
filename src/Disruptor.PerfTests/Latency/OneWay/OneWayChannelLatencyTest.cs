using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;
using Disruptor.Util;
using HdrHistogram;

namespace Disruptor.PerfTests.Latency.OneWay;

public class OneWayChannelLatencyTest : ILatencyTest, IExternalTest
{
    private const int _bufferSize = 1024;
    private const long _iterations = 100 * 1000 * 30;
    private static readonly long _pause = StopwatchUtil.GetTimestampFromMicroseconds(10);

    private readonly Producer _producer;
    private readonly Consumer _consumer;

    public OneWayChannelLatencyTest()
    {
        var channel = Channel.CreateBounded<PerfEvent>(new BoundedChannelOptions(_bufferSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
        });
        _producer = new Producer(channel);
        _consumer = new Consumer(channel);
    }

    public int RequiredProcessorCount => 2;

    public void Run(LatencySessionContext sessionContext)
    {
        var startSignal = new ManualResetEventSlim();
        var producerTask = _producer.Start(startSignal);
        var consumerTask = _consumer.Start(sessionContext.Histogram);

        _producer.Started.Wait();
        _consumer.Started.Wait();

        sessionContext.Start();

        startSignal.Set();

        producerTask.Wait();
        consumerTask.Wait();

        sessionContext.Stop();
    }

    private class Producer
    {
        private readonly Channel<PerfEvent> _channel;

        public Producer(Channel<PerfEvent> channel)
        {
            _channel = channel;
        }

        public ManualResetEventSlim Started { get; } = new();

        public Task Start(ManualResetEventSlim startSignal)
        {
            Started.Reset();

            return Task.Run(() => Run(startSignal));
        }

        private async Task Run(ManualResetEventSlim startSignal)
        {
            Started.Set();

            startSignal.Wait();

            var pause = _pause;
            var next = Stopwatch.GetTimestamp() + pause;

            for (int i = 0; i < _iterations; i++)
            {
                var now = Stopwatch.GetTimestamp();
                while (now < next)
                {
                    Thread.Yield();
                    now = Stopwatch.GetTimestamp();
                }

                await _channel.Writer.WriteAsync(new PerfEvent { Value = now });

                next = now + pause;
            }

            await _channel.Writer.WriteAsync(new PerfEvent { Value = -1 });
        }
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

        public Task Start(HistogramBase histogram)
        {
            Started.Reset();
            _histogram = histogram;

            return Task.Run(Run);
        }

        private async Task Run()
        {
            Started.Set();

            while (await _channel.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (_channel.Reader.TryRead(out var perfEvent))
                {
                    if (perfEvent.Value == -1)
                        return;

                    var consumerTimestamp = Stopwatch.GetTimestamp();
                    var duration = consumerTimestamp - perfEvent.Value;

                    _histogram.RecordValue(StopwatchUtil.ToNanoseconds(duration));
                }
            }
        }
    }
}
