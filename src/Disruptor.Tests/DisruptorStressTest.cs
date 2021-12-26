using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class DisruptorStressTest
    {
        [Test]
        public void ShouldHandleLotsOfThreads()
        {
            var disruptor = new Disruptor<TestEvent>(TestEvent.Factory, 65_536, TaskScheduler.Current, ProducerType.Multi, new BusySpinWaitStrategy());
            var ringBuffer = disruptor.RingBuffer;
            disruptor.SetDefaultExceptionHandler(new FatalExceptionHandler());

            var threads = Math.Max(1, Environment.ProcessorCount / 2);

            const int iterations = 20_000_000;
            var publisherCount = threads;
            var handlerCount = threads;

            var end = new CountdownEvent(publisherCount);
            var start = new CountdownEvent(publisherCount);

            var handlers = Initialise(disruptor, new TestEventHandler[handlerCount]);
            var publishers = Initialise(new Publisher[publisherCount], ringBuffer, iterations, start, end);

            disruptor.Start();

            foreach (var publisher in publishers)
            {
                Task.Run(publisher.Run);
            }

            end.Wait();

            var spinWait = new AggressiveSpinWait();

            while (ringBuffer.Cursor < (iterations - 1))
            {
                spinWait.SpinOnce();
            }

            disruptor.Shutdown();

            foreach (var publisher in publishers)
            {
                Assert.That(publisher.Failed, Is.EqualTo(false));
            }

            foreach (var handler in handlers)
            {
                Assert.That(handler.MessagesSeen, Is.Not.EqualTo(0));
                Assert.That(handler.FailureCount, Is.EqualTo(0));
            }
        }

        private Publisher[] Initialise(Publisher[] publishers, RingBuffer<TestEvent> buffer, int messageCount, CountdownEvent start, CountdownEvent end)
        {
            for (var i = 0; i < publishers.Length; i++)
            {
                publishers[i] = new Publisher(buffer, messageCount, start, end);
            }

            return publishers;
        }

        private TestEventHandler[] Initialise(Disruptor<TestEvent> disruptor, TestEventHandler[] testEventHandlers)
        {
            for (var i = 0; i < testEventHandlers.Length; i++)
            {
                var handler = new TestEventHandler();
                disruptor.HandleEventsWith(handler);
                testEventHandlers[i] = handler;
            }

            return testEventHandlers;
        }

        private class TestEventHandler : IEventHandler<TestEvent>
        {
            public int FailureCount;
            public int MessagesSeen;

            public void OnEvent(TestEvent @event, long sequence, bool endOfBatch)
            {
                if (@event.Sequence != sequence || @event.A != sequence + 13 || @event.B != sequence - 7)
                {
                    FailureCount++;
                }

                MessagesSeen++;
            }
        }

        private class Publisher
        {
            private readonly RingBuffer<TestEvent> _ringBuffer;
            private readonly CountdownEvent _end;
            private readonly CountdownEvent _start;
            private readonly int _iterations;

            public bool Failed;

            public Publisher(RingBuffer<TestEvent> ringBuffer, int iterations, CountdownEvent start, CountdownEvent end)
            {
                _ringBuffer = ringBuffer;
                _end = end;
                _start = start;
                _iterations = iterations;
            }

            public void Run()
            {
                try
                {
                    _start.Signal();
                    _start.Wait();

                    var i = _iterations;
                    while (--i != -1)
                    {
                        var next = _ringBuffer.Next();
                        var testEvent = _ringBuffer[next];
                        testEvent.Sequence = next;
                        testEvent.A = next + 13;
                        testEvent.B = next - 7;
                        _ringBuffer.Publish(next);
                    }
                }
                catch (Exception)
                {
                    Failed = true;
                }
                finally
                {
                    _end.Signal();
                }
            }
        }

        public class TestEvent
        {
            public static readonly Func<TestEvent> Factory = () => new TestEvent();

            public long Sequence;
            public long A;
            public long B;

            // The string member was removed because it was not really useful for the test
            // but the allocations made the test too slow.
        }
    }
}
