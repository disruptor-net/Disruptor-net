using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;

namespace Disruptor.PerfTests.Queue
{
    public class OneToOneConcurrentQueueThroughputTest : IThroughputTest, IQueueTest
    {
        private const int _bufferSize = 1024 * 64;
        private const long _iterations = 1000L * 1000L * 100L;

        private readonly long _expectedResult = PerfTestUtil.AccumulatedAddition(_iterations);

        private readonly ManualResetEvent _latch;
        private readonly ConcurrentQueue<PerfEvent> _queue;
        private readonly AdditionEventHandler _eventHandler;
        private readonly Consumer _consumer;

        public OneToOneConcurrentQueueThroughputTest()
        {
            _latch = new ManualResetEvent(false);
            _queue = new ConcurrentQueue<PerfEvent>();
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

            var spinWait = new SpinWait();
            for (long i = 0; i < _iterations; i++)
            {
                var data = new PerfEvent { Value = i };
                while (_queue.Count == _bufferSize)
                {
                    spinWait.SpinOnce();
                }
                _queue.Enqueue(data);
                spinWait.Reset();
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
            private readonly ConcurrentQueue<PerfEvent> _queue;
            private readonly AdditionEventHandler _eventHandler;
            private volatile bool _running;
            private Task _task;

            public Consumer(ConcurrentQueue<PerfEvent> queue, AdditionEventHandler eventHandler)
            {
                _queue = queue;
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

                        while (!_queue.TryDequeue(out perfEvent))
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
