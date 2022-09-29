using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;

namespace Disruptor.PerfTests.External;

public class OneToOneChannelAsyncThroughputTest : IThroughputTest, IExternalTest
{
    private const int _bufferSize = 1024 *  64;
    private const long _iterations = 1000L * 1000L * 100L;

    private readonly long _expectedResult = PerfTestUtil.AccumulatedAddition(_iterations);

    private readonly AdditionEventHandler _eventHandler;
    private Channel<PerfEvent> _channel;
    private Consumer _consumer;

    public OneToOneChannelAsyncThroughputTest()
    {
        _eventHandler = new AdditionEventHandler();
    }

    public int RequiredProcessorCount => 2;

    public long Run(ThroughputSessionContext sessionContext)
    {
        _channel = Channel.CreateBounded<PerfEvent>(new BoundedChannelOptions(_bufferSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
        });
        _consumer = new Consumer(_channel.Reader, _eventHandler);

        _eventHandler.Reset(_iterations - 1);
        _consumer.Start();

        var producerSignal = new ManualResetEventSlim();
        var producer = Task.Run(async () =>
        {
            producerSignal.Wait();
            await PublishOneByOne();
            // await PublishBatchedV1();
            // await PublishBatchedV2();
        });

        sessionContext.Start();

        producerSignal.Set();
        _eventHandler.WaitForSequence();

        sessionContext.Stop();

        _channel.Writer.Complete();
        producer.Wait();
        _consumer.Stop();

        sessionContext.SetBatchData(_eventHandler.BatchesProcessed, _iterations);

        PerfTestUtil.FailIfNot(_expectedResult, _eventHandler.Value, $"Handler should have processed {_expectedResult} events, but was: {_eventHandler.Value}");

        return _iterations;
    }

    private async Task PublishOneByOne()
    {
        for (long i = 0; i < _iterations; i++)
        {
            var data = new PerfEvent { Value = i };
            await _channel.Writer.WriteAsync(data).ConfigureAwait(false);
        }
    }

    private async Task PublishBatchedV1()
    {
        var i = 0;
        while (i < _iterations)
        {
            await _channel.Writer.WaitToWriteAsync().ConfigureAwait(false);

            while (i < _iterations && _channel.Writer.TryWrite(new PerfEvent { Value = i }))
            {
                i++;
            }
        }
    }

    private async Task PublishBatchedV2()
    {
        for (long i = 0; i < _iterations; i++)
        {
            var data = new PerfEvent { Value = i };
            if (!_channel.Writer.TryWrite(data))
                await _channel.Writer.WriteAsync(data).ConfigureAwait(false);
        }
    }

    private class Consumer
    {
        private readonly ChannelReader<PerfEvent> _channelReader;
        private readonly AdditionEventHandler _eventHandler;
        private Task _task;

        public Consumer(ChannelReader<PerfEvent> channelReader, AdditionEventHandler eventHandler)
        {
            _channelReader = channelReader;
            _eventHandler = eventHandler;
        }

        public void Start()
        {
            var started = new ManualResetEventSlim();

            _task = Task.Run(async () =>
            {
                started.Set();

                while (await _channelReader.WaitToReadAsync().ConfigureAwait(false))
                {
                    while (_channelReader.TryRead(out var perfEvent))
                    {
                        _eventHandler.OnBatchStart(1);
                        _eventHandler.OnEvent(perfEvent, perfEvent.Value, true);
                    }
                }
            });

            started.Wait();
        }

        public void Stop()
        {
            _task.Wait();
        }
    }
}
