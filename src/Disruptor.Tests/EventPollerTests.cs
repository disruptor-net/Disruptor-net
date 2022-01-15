using System;
using System.Collections.Generic;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture]
public class EventPollerTests
{
    [Test]
    public void ShouldPollForEvents()
    {
        var gatingSequence = new Sequence();
        var sequencer = new SingleProducerSequencer(16, new BusySpinWaitStrategy());

        bool Handler(object e, long s, bool b) => false;

        var provider = new ArrayDataProvider<object>(new object[16]);
        provider.Data[0] = new object();

        var poller = sequencer.NewPoller(provider, gatingSequence);

        Assert.That(poller.Poll(Handler), Is.EqualTo(EventPoller.PollState.Idle));

        // Publish Event.
        sequencer.Publish(sequencer.Next());
        Assert.That(poller.Poll(Handler), Is.EqualTo(EventPoller.PollState.Gating));

        gatingSequence.IncrementAndGet();

        Assert.That(poller.Poll(Handler), Is.EqualTo(EventPoller.PollState.Processing));
    }

    [Test]
    public void ShouldPollForEvents_Batch()
    {
        var gatingSequence = new Sequence();
        var sequencer = new SingleProducerSequencer(16, new BusySpinWaitStrategy());

        void Handler(EventBatch<object> batch, long s)
        {
        }

        var provider = new ArrayDataProvider<object>(new object[16]);
        provider.Data[0] = new object();

        var poller = sequencer.NewPoller(provider, gatingSequence);

        Assert.That(poller.Poll(Handler), Is.EqualTo(EventPoller.PollState.Idle));

        // Publish Event.
        sequencer.Publish(sequencer.Next());
        Assert.That(poller.Poll(Handler), Is.EqualTo(EventPoller.PollState.Gating));

        gatingSequence.IncrementAndGet();

        Assert.That(poller.Poll(Handler), Is.EqualTo(EventPoller.PollState.Processing));
    }

    [Test]
    public void ShouldSuccessfullyPollWhenBufferIsFull()
    {
        var events = new List<byte[]>();

        byte[] Factory() => new byte[1];

        bool Handler(byte[] data, long sequence, bool endOfBatch)
        {
            events.Add(data);
            return !endOfBatch;
        }

        var ringBuffer = RingBuffer<byte[]>.CreateMultiProducer(Factory, 4, new SleepingWaitStrategy());

        var poller = ringBuffer.NewPoller();
        ringBuffer.AddGatingSequences(poller.Sequence);

        const int count = 4;

        for (byte i = 1; i <= count; ++i)
        {
            var next = ringBuffer.Next();
            ringBuffer[next][0] = i;
            ringBuffer.Publish(next);
        }

        // think of another thread
        poller.Poll(Handler);

        Assert.That(events.Count, Is.EqualTo(4));
    }

    [Test]
    public void ShouldSuccessfullyPollWhenBufferIsFull_Batch()
    {
        var batches = new List<byte[][]>();

        byte[] Factory() => new byte[1];

        void Handler(EventBatch<byte[]> batch, long sequence) => batches.Add(batch.ToArray());

        var ringBuffer = RingBuffer<byte[]>.CreateMultiProducer(Factory, 4, new SleepingWaitStrategy());

        var poller = ringBuffer.NewPoller();
        ringBuffer.AddGatingSequences(poller.Sequence);

        const int count = 4;

        for (byte i = 1; i <= count; ++i)
        {
            var next = ringBuffer.Next();
            ringBuffer[next][0] = i;
            ringBuffer.Publish(next);
        }

        // think of another thread
        poller.Poll(Handler);

        Assert.That(batches.Count, Is.EqualTo(1));
        Assert.That(batches[0].Length, Is.EqualTo(4));
    }
}