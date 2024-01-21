using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture]
public class DisruptorStressTest
{
    [Test]
    public void ShouldHandleLotsOfThreads_EventHandler()
    {
        ShouldHandleLotsOfThreads<TestEventHandler>(new BusySpinWaitStrategy(), 20_000_000);
    }

    [Test]
    public void ShouldHandleLotsOfThreads_BatchEventHandler()
    {
        ShouldHandleLotsOfThreads<TestBatchEventHandler>(new BusySpinWaitStrategy(), 20_000_000);
    }

    [Test]
    public void ShouldHandleLotsOfThreads_AsyncBatchEventHandler()
    {
        ShouldHandleLotsOfThreads<TestAsyncBatchEventHandler>(new AsyncWaitStrategy(), 2_000_000);
    }

    private static void ShouldHandleLotsOfThreads<T>(IWaitStrategy waitStrategy, int iterations) where T : ITestHandler, new()
    {
        var disruptor = new Disruptor<TestEvent>(TestEvent.Factory, 65_536, TaskScheduler.Current, ProducerType.Multi, waitStrategy);
        var ringBuffer = disruptor.RingBuffer;
        disruptor.SetDefaultExceptionHandler(new FatalExceptionHandler<TestEvent>());

        var publisherCount = Math.Clamp(Environment.ProcessorCount / 2, 1, 8);
        var handlerCount = Math.Clamp(Environment.ProcessorCount / 2, 1, 8);

        var start = new CountdownEvent(publisherCount);

        var handlers = new T[handlerCount];
        for (var i = 0; i < handlers.Length; i++)
        {
            var handler = new T();
            handler.Register(disruptor);
            handlers[i] = handler;
        }

        var publishers = new Publisher[publisherCount];
        for (var i = 0; i < publishers.Length; i++)
        {
            publishers[i] = new Publisher(ringBuffer, iterations, start);
        }

        disruptor.Start();

        var publisherTasks = publishers.Select(x => Task.Run(x.Run)).ToArray();
        Task.WaitAll(publisherTasks);

        disruptor.Shutdown(TimeSpan.FromSeconds(10));

        foreach (var publisher in publishers)
        {
            Assert.That(publisher.Failed, Is.EqualTo(false));
        }

        foreach (var handler in handlers)
        {
            Assert.That(handler.MessagesSeen, Is.EqualTo(iterations * publishers.Length));
            Assert.That(handler.FailureCount, Is.EqualTo(0));
        }
    }

    private interface ITestHandler
    {
        int FailureCount { get; }
        int MessagesSeen { get; }

        void Register(Disruptor<TestEvent> disruptor);
    }

    private class TestEventHandler : IEventHandler<TestEvent>, ITestHandler
    {
        public int FailureCount { get; private set; }
        public int MessagesSeen { get; private set; }

        public void Register(Disruptor<TestEvent> disruptor)
        {
            disruptor.HandleEventsWith(this);
        }

        public void OnEvent(TestEvent @event, long sequence, bool endOfBatch)
        {
            if (@event.Sequence != sequence || @event.A != sequence + 13 || @event.B != sequence - 7)
            {
                FailureCount++;
            }

            MessagesSeen++;
        }
    }

    private class TestBatchEventHandler : IBatchEventHandler<TestEvent>, ITestHandler
    {
        public int FailureCount { get; private set; }
        public int MessagesSeen { get; private set; }

        public void Register(Disruptor<TestEvent> disruptor)
        {
            disruptor.HandleEventsWith(this);
        }

        public void OnBatch(EventBatch<TestEvent> batch, long sequence)
        {
            for (var i = 0; i < batch.Length; i++)
            {
                var @event = batch[i];
                var s = sequence + i;

                if (@event.Sequence != s || @event.A != s + 13 || @event.B != s - 7)
                {
                    FailureCount++;
                }

                MessagesSeen++;
            }
        }
    }

    private class TestAsyncBatchEventHandler : IAsyncBatchEventHandler<TestEvent>, ITestHandler
    {
        public int FailureCount { get; private set; }
        public int MessagesSeen { get; private set; }

        public void Register(Disruptor<TestEvent> disruptor)
        {
            disruptor.HandleEventsWith(this);
        }

        public async ValueTask OnBatch(EventBatch<TestEvent> batch, long sequence)
        {
            for (var i = 0; i < batch.Length; i++)
            {
                var @event = batch[i];
                var s = sequence + i;

                if (@event.Sequence != s || @event.A != s + 13 || @event.B != s - 7)
                {
                    FailureCount++;
                }

                MessagesSeen++;
            }

            await Task.Yield();
        }
    }

    private class Publisher
    {
        private readonly RingBuffer<TestEvent> _ringBuffer;
        private readonly CountdownEvent _start;
        private readonly int _iterations;

        public bool Failed;

        public Publisher(RingBuffer<TestEvent> ringBuffer, int iterations, CountdownEvent start)
        {
            _ringBuffer = ringBuffer;
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
                    var sequence = _ringBuffer.Next();
                    var testEvent = _ringBuffer[sequence];
                    testEvent.Sequence = sequence;
                    testEvent.A = sequence + 13;
                    testEvent.B = sequence - 7;
                    _ringBuffer.Publish(sequence);
                }
            }
            catch (Exception)
            {
                Failed = true;
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
