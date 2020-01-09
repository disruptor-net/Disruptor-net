using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;

namespace Disruptor.PerfTests.Queue
{
    public class OneToOneBlockingCollectionThroughputTest : IThroughputTest, IQueueTest
    {
        private const int _bufferSize = 1024 * 64;
        private const long _iterations = 1000L * 1000L * 100L;

        private readonly long _expectedResult = PerfTestUtil.AccumulatedAddition(_iterations);

        private readonly ManualResetEvent _latch;
        private readonly BlockingCollection<PerfEvent> _queue;
        private readonly AdditionEventHandler _eventHandler;
        private readonly Consumer _consumer;

        public OneToOneBlockingCollectionThroughputTest()
        {
            _latch = new ManualResetEvent(false);
            _queue = new BlockingCollection<PerfEvent>(_bufferSize);
            _eventHandler = new AdditionEventHandler();
            _consumer = new Consumer(_queue, _eventHandler);
        }

        public int RequiredProcessorCount => 2;

        public long Run(ThroughputSessionContext sessionContext)
        {
            _latch.Reset();
            _eventHandler.Reset(_latch, _iterations - 1);
            _consumer.Start();

            sessionContext.Start();

            for (long i = 0; i < _iterations; i++)
            {
                var data = new PerfEvent { Value = i };
                _queue.Add(data);
            }

            _latch.WaitOne();
            sessionContext.Stop();
            _consumer.Stop();

            sessionContext.SetBatchData(_eventHandler.BatchesProcessedCount.Value, _iterations);

            PerfTestUtil.FailIfNot(_expectedResult, _eventHandler.Value, $"Handler should have processed {_expectedResult} events, but was: {_eventHandler.Value}");

            return _iterations;
        }

        private class Consumer
        {
            private readonly BlockingCollection<PerfEvent> _queue;
            private readonly AdditionEventHandler _eventHandler;
            private Task _task;
            private CancellationTokenSource _cancellationTokenSource;

            public Consumer(BlockingCollection<PerfEvent> queue, AdditionEventHandler eventHandler)
            {
                _queue = queue;
                _eventHandler = eventHandler;
            }

            public void Start()
            {
                var started = new ManualResetEventSlim();

                _cancellationTokenSource = new CancellationTokenSource();
                _task = Task.Run(() =>
                {
                    started.Set();

                    try
                    {
                        foreach (var perfEvent in _queue.GetConsumingEnumerable(_cancellationTokenSource.Token))
                        {
                            _eventHandler.OnBatchStart(1);
                            _eventHandler.OnEvent(perfEvent, perfEvent.Value, true);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                });

                started.Wait();
            }

            public void Stop()
            {
                _cancellationTokenSource.Cancel();
                _task.Wait();
            }
        }
    }
}
