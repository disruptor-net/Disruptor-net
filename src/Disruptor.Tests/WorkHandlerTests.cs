using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.Processing;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture]
public class WorkHandlerTests
{
    [Test]
    public void RemoveWorkHandlerLostEventExample()
    {
        int eventSize = 8;
        var countdownEvent = new CountdownEvent(eventSize);

        var workSequence = new Sequence();

        var disruptor = new Disruptor<StubEvent>(StubEvent.EventFactory, 4);
        disruptor.Start();

        var ringBuffer = disruptor.RingBuffer;

        var handler1 = new DynamicHandler(1, countdownEvent);
        var processor1 = new WorkProcessor<StubEvent>(ringBuffer, ringBuffer.NewBarrier(), handler1, new FatalExceptionHandler<StubEvent>(), workSequence);

        var handler2 = new DynamicHandler(2, countdownEvent);
        var processor2 = new WorkProcessor<StubEvent>(ringBuffer, ringBuffer.NewBarrier(), handler2, new FatalExceptionHandler<StubEvent>(), workSequence);

        ringBuffer.AddGatingSequences(processor1.Sequence);
        Task.Run(() => processor1.Run());

        ringBuffer.AddGatingSequences(processor2.Sequence);
        Task.Run(() => processor2.Run());

        handler1.AwaitStart();
        handler2.AwaitStart();

        Thread.Sleep(100);

        // processor1 should own an unavailable work sequence
        // => this sequence will be dropped by Halt
        processor1.Halt();

        var producer = new MessageProducer(disruptor, InitData(0, eventSize));
        Task.Run(() => producer.Run());
        producer.AwaitStart();

        handler1.AwaitShutdown();

        ringBuffer.RemoveGatingSequence(processor1.Sequence);

        // countdownEvent should not reach zero because of the dropped sequence
        var await = countdownEvent.Wait(TimeSpan.FromMilliseconds(500));
        Assert.That(!await);
    }

    [Test]
    public void RemoveWorkHandlerLaterTest()
    {
        var eventSize = 8;
        var countdownEvent = new CountdownEvent(eventSize);

        var workSequence = new Sequence();

        var disruptor = new Disruptor<StubEvent>(StubEvent.EventFactory, 4);
        disruptor.Start();

        var ringBuffer = disruptor.RingBuffer;

        var handler1 = new DynamicHandler(1, countdownEvent);
        var processor1 = new WorkProcessor<StubEvent>(ringBuffer, ringBuffer.NewBarrier(), handler1, new FatalExceptionHandler<StubEvent>(), workSequence);

        var handler2 = new DynamicHandler(2, countdownEvent);
        var processor2 = new WorkProcessor<StubEvent>(ringBuffer, ringBuffer.NewBarrier(), handler2, new FatalExceptionHandler<StubEvent>(), workSequence);

        ringBuffer.AddGatingSequences(processor1.Sequence);
        Task.Run(() => processor1.Run());

        ringBuffer.AddGatingSequences(processor2.Sequence);
        Task.Run(() => processor2.Run());

        handler1.AwaitStart();
        handler2.AwaitStart();

        Thread.Sleep(100);

        // processor1 should own an unavailable work sequence
        // => this sequence should not be dropped by HaltLater
        processor1.HaltLater();

        var producer = new MessageProducer(disruptor, InitData(0, eventSize));
        Task.Run(() => producer.Run());
        producer.AwaitStart();

        handler1.AwaitShutdown();

        ringBuffer.RemoveGatingSequence(processor1.Sequence);

        Assert.That(countdownEvent.Wait(TimeSpan.FromSeconds(3)));
    }

    private static List<int> InitData(int start, int eventSize)
    {
        return Enumerable.Range(start, eventSize).ToList();
    }

    private class MessageProducer
    {
        private readonly CountdownEvent _startLatch = new(1);
        private readonly Disruptor<StubEvent> _disruptor;
        private readonly List<int> _dataSet;

        public MessageProducer(Disruptor<StubEvent> disruptor, List<int> dataSet)
        {
            _disruptor = disruptor;
            _dataSet = dataSet;
        }

        public void Run()
        {
            _startLatch.Signal();

            foreach (var i in _dataSet)
            {
                using (var scope = _disruptor.PublishEvent())
                {
                    scope.Event().Value = i;
                }

                Thread.Yield();
            }
        }

        public void AwaitStart()
        {
            _startLatch.Wait();
        }
    }

    private class DynamicHandler : IWorkHandler<StubEvent>
    {
        private readonly CountdownEvent _shutdownLatch = new(1);
        private readonly CountdownEvent _startLatch = new(1);
        private readonly int _id;
        private readonly CountdownEvent _countdownEvent;

        public DynamicHandler(int id, CountdownEvent countdownEvent)
        {
            _id = id;
            _countdownEvent = countdownEvent;
        }

        public void OnStart()
        {
            _startLatch.Signal();
        }

        public void OnShutdown()
        {
            _shutdownLatch.Signal();
        }

        public void AwaitShutdown()
        {
            _shutdownLatch.Wait();
        }

        public void AwaitStart()
        {
            _startLatch.Wait();
        }

        public void OnEvent(StubEvent evt)
        {
            _countdownEvent.Signal();
            Thread.Yield();
        }
    }
}
