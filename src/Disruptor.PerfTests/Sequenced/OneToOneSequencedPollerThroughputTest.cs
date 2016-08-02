using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.PerfTests.Support;
using Disruptor.Tests.Support;

namespace Disruptor.PerfTests.Sequenced
{
    public class OneToOneSequencedPollerThroughputTest : IThroughputTest
    {
        private const int _bufferSize = 1024 * 64;
        private const long _iterations = 1000L * 1000L * 100L;
        private static readonly IExecutor _executor = new BasicExecutor(TaskScheduler.Current);

        ///////////////////////////////////////////////////////////////////////////////////////////////

        private readonly RingBuffer<ValueEvent> _ringBuffer;
        private readonly EventPoller<ValueEvent> _poller;
        private readonly PollRunnable _pollRunnable;
        private readonly long _expectedResult = PerfTestUtil.AccumulatedAddition(_iterations);

        public OneToOneSequencedPollerThroughputTest()
        {
            _ringBuffer = RingBuffer<ValueEvent>.CreateSingleProducer(ValueEvent.EventFactory, _bufferSize, new YieldingWaitStrategy());
            _poller = _ringBuffer.NewPoller();
            _ringBuffer.AddGatingSequences(_poller.Sequence);
            _pollRunnable = new PollRunnable(_poller);
        }

        public int RequiredProcessorCount => 2;

        ///////////////////////////////////////////////////////////////////////////////////////////////

        public class PollRunnable
        {
            private readonly EventPoller<ValueEvent> _poller;
            private readonly Func<ValueEvent, long, bool, bool> _eventHandler;
            private volatile int _running = 1;
            private PaddedLong _value;
            private ManualResetEvent _signal;
            private long _count;

            public PollRunnable(EventPoller<ValueEvent> poller)
            {
                _poller = poller;
                _eventHandler = OnEvent;
            }

            public long Value => _value.Value;

            public void Run()
            {
                try
                {
                    while (_running == 1)
                    {
                        if (PollState.Processing != _poller.Poll(_eventHandler))
                        {
                            Thread.Sleep(0);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            private bool OnEvent(ValueEvent @event, long sequence, bool endOfBatch)
            {
                _value.Value = _value.Value + @event.Value;

                if (_count == sequence)
                {
                    _signal.Set();
                }

                return true;
            }

            public void Halt() => Interlocked.Exchange(ref _running, 0);

            public void Reset(ManualResetEvent signal, long expectedCount)
            {
                _value.Value = 0L;
                _signal = signal;
                _count = expectedCount;
                _running = 1;
            }
        }

        public long Run(Stopwatch stopwatch)
        {
            var latch = new ManualResetEvent(false);
            var expectedCount = _poller.Sequence.Value + _iterations;
            _pollRunnable.Reset(latch, expectedCount);
            var processorTask = _executor.Execute(_pollRunnable.Run);
            stopwatch.Start();

            var rb = _ringBuffer;
            for (var i = 0; i < _iterations; i++)
            {
                var next = rb.Next();
                rb[next].Value = i;
                rb.Publish(next);
            }

            latch.WaitOne();
            stopwatch.Stop();
            WaitForEventProcessorSequence(expectedCount);
            _pollRunnable.Halt();
            processorTask.Wait(2000);

            PerfTestUtil.FailIfNot(_expectedResult, _pollRunnable.Value, $"Poll runnable should have processed {_expectedResult} but was {_pollRunnable.Value}");

            return _iterations;
        }

        private void WaitForEventProcessorSequence(long expectedCount)
        {
            while (_poller.Sequence.Value != expectedCount)
            {
                Thread.Sleep(1);
            }
        }
    }
}