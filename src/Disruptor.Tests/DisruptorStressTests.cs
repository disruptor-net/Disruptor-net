using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture]
public class DisruptorStressTests
{
    [Test]
    public void ShouldHandleLotsOfThreads_EventHandler()
    {
        ShouldHandleLotsOfThreads<TestEventHandler>(new BusySpinWaitStrategy(), 10_000_000, TimeoutOptions.None);
    }

    [Test]
    public void ShouldHandleLotsOfThreads_EventHandler_Timeout()
    {
        var timeout = TimeSpan.FromMilliseconds(20);
        var timeoutOptions = new TimeoutOptions(50, timeout);
        ShouldHandleLotsOfThreads<TestEventHandler>(new TimeoutBlockingWaitStrategy(timeout), 2_000_000, timeoutOptions);
    }

    [Test]
    public void ShouldHandleLotsOfThreads_BatchEventHandler()
    {
        ShouldHandleLotsOfThreads<TestBatchEventHandler>(new BusySpinWaitStrategy(), 10_000_000, TimeoutOptions.None);
    }

    [Test]
    public void ShouldHandleLotsOfThreads_AsyncBatchEventHandler()
    {
        ShouldHandleLotsOfThreads<TestAsyncBatchEventHandler>(new AsyncWaitStrategy(), 2_000_000, TimeoutOptions.None);
    }

    [Test]
    public void ShouldHandleLotsOfThreads_AsyncBatchEventHandler_Timeout()
    {
        var timeout = TimeSpan.FromMilliseconds(20);
        var timeoutOptions = new TimeoutOptions(50, timeout);
        ShouldHandleLotsOfThreads<TestAsyncBatchEventHandler>(new TimeoutAsyncWaitStrategy(timeout), 1_000_000, timeoutOptions);
    }

    private static void ShouldHandleLotsOfThreads<T>(IWaitStrategy waitStrategy, int iterations, TimeoutOptions timeoutOptions) where T : IHandler, new()
    {
        using var disruptor = new Disruptor<TestEvent>(TestEvent.Factory, 65_536, TaskScheduler.Current, ProducerType.Multi, waitStrategy);
        var ringBuffer = disruptor.RingBuffer;
        disruptor.SetDefaultExceptionHandler(new FatalExceptionHandler<TestEvent>());

        var publisherCount = Math.Clamp(Environment.ProcessorCount / 2, 1, 8);
        var handlerCount = Math.Clamp(Environment.ProcessorCount / 2, 1, 8);

        var end = new CountdownEvent(publisherCount);
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
            publishers[i] = new Publisher(ringBuffer, iterations, start, end);
        }

        disruptor.Start();

        Thread.Sleep(timeoutOptions.TimoutCount * timeoutOptions.TimeoutDelay);

        foreach (var publisher in publishers)
        {
            Task.Run(publisher.Run);
        }

        end.Wait();

        var spinWait = new SpinWait();

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
            // TimerCount is off on GitHub macOS and Windows image builds because the default timer resolution is too large.
            var expectedTimeoutCount = OperatingSystem.IsLinux() ? timeoutOptions.TimoutCount * 0.5 : timeoutOptions.TimoutCount * 0.1;
            Assert.That(handler.TimeoutCount, Is.GreaterThanOrEqualTo(expectedTimeoutCount));

            Assert.That(handler.MessagesSeen, Is.Not.EqualTo(0));
            Assert.That(handler.FailureCount, Is.EqualTo(0));
        }
    }

    private interface IHandler
    {
        int FailureCount { get; }
        int MessagesSeen { get; }
        int TimeoutCount { get; }

        void Register(Disruptor<TestEvent> disruptor);
    }

    private class TestEventHandler : IEventHandler<TestEvent>, IHandler
    {
        public int FailureCount { get; private set; }
        public int MessagesSeen { get; private set; }
        public int TimeoutCount { get; private set; }

        public void Register(Disruptor<TestEvent> disruptor)
        {
            disruptor.HandleEventsWith(this);
        }

        public void OnTimeout(long sequence)
        {
            TimeoutCount++;
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

    private class TestBatchEventHandler : IBatchEventHandler<TestEvent>, IHandler
    {
        public int FailureCount { get; private set; }
        public int MessagesSeen { get; private set; }
        public int TimeoutCount { get; private set; }

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

    private class TestAsyncBatchEventHandler : IAsyncBatchEventHandler<TestEvent>, IHandler
    {
        public int FailureCount { get; private set; }
        public int MessagesSeen { get; private set; }

        public int TimeoutCount { get; private set; }

        public void Register(Disruptor<TestEvent> disruptor)
        {
            disruptor.HandleEventsWith(this);
        }

        public void OnTimeout(long sequence)
        {
            TimeoutCount++;
            Console.WriteLine($"Timeout! {DateTime.UtcNow:HH:mm:ss.fff}");
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

    private record TimeoutOptions(int TimoutCount, TimeSpan TimeoutDelay)
    {
        public static TimeoutOptions None { get; } = new TimeoutOptions(0, TimeSpan.Zero);
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
