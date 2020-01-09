using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;

namespace Disruptor.PerfTests.Queue
{
    public class OneToOneChannelThroughputTest : IThroughputTest, IQueueTest
    {
        private const int _bufferSize = 1024 * 64;
        private const long _iterations = 1000L * 1000L * 100L;

        private readonly long _expectedResult = PerfTestUtil.AccumulatedAddition(_iterations);

        private readonly Channel<PerfEvent> _channel;
        private readonly AdditionEventHandler _eventHandler;
        private readonly Consumer _consumer;

        public OneToOneChannelThroughputTest()
        {
            _channel = Channel.CreateBounded<PerfEvent>(new BoundedChannelOptions(_bufferSize)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
            });
            _eventHandler = new AdditionEventHandler();
            _consumer = new Consumer(_channel.Reader, _eventHandler);
        }

        public int RequiredProcessorCount => 2;

        public long Run(ThroughputSessionContext sessionContext)
        {
            _eventHandler.Reset(_iterations - 1);
            _consumer.Start();

            sessionContext.Start();

            var spinWait = new SpinWait();
            for (long i = 0; i < _iterations; i++)
            {
                var data = new PerfEvent { Value = i };
                while (!_channel.Writer.TryWrite(data))
                {
                    spinWait.SpinOnce();
                }
                spinWait.Reset();
            }

            _eventHandler.Latch.WaitOne();
            sessionContext.Stop();
            _consumer.Stop();

            sessionContext.SetBatchData(_eventHandler.BatchesProcessedCount.Value, _iterations);

            PerfTestUtil.FailIfNot(_expectedResult, _eventHandler.Value, $"Handler should have processed {_expectedResult} events, but was: {_eventHandler.Value}");

            return _iterations;
        }

        private class Consumer
        {
            private readonly ChannelReader<PerfEvent> _channelReader;
            private readonly AdditionEventHandler _eventHandler;
            private volatile bool _running;
            private Task _task;

            public Consumer(ChannelReader<PerfEvent> channelReader, AdditionEventHandler eventHandler)
            {
                _channelReader = channelReader;
                _eventHandler = eventHandler;
            }

            public void Start()
            {
                var started = new ManualResetEventSlim();

                _running = true;
                _task = Task.Run(() =>
                {
                    started.Set();

                    var spinWait = new SpinWait();
                    while (true)
                    {
                        PerfEvent perfEvent;

                        while (!_channelReader.TryRead(out perfEvent))
                        {
                            if (!_running)
                                return;

                            spinWait.SpinOnce();
                        }

                        spinWait.Reset();

                        _eventHandler.OnBatchStart(1);
                        _eventHandler.OnEvent(perfEvent, perfEvent.Value, true);
                    }
                });

                started.Wait();
            }

            public void Stop()
            {
                _running = false;
                _task.Wait();
            }
        }
    }
}
