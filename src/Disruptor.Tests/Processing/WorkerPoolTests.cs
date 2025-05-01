﻿using System;
using System.Threading;
using Disruptor.Processing;
using NUnit.Framework;

namespace Disruptor.Tests.Processing;

[TestFixture]
public class WorkerPoolTests
{
    [Test]
    public void ShouldProcessEachMessageByOnlyOneWorker()
    {
        var ringBuffer = RingBuffer<AtomicLong>.CreateMultiProducer(() => new AtomicLong(), 1024, new BlockingWaitStrategy());
        var pool = new WorkerPool<AtomicLong>(ringBuffer,
                                              Array.Empty<Sequence>(),
                                              new FatalExceptionHandler<AtomicLong>(),
                                              new AtomicLongWorkHandler(),
                                              new AtomicLongWorkHandler());

        pool.Start();

        ringBuffer.Next();
        ringBuffer.Next();
        ringBuffer.Publish(0);
        ringBuffer.Publish(1);

        Thread.Sleep(500);

        Assert.That(ringBuffer[0].Value, Is.EqualTo(1L));
        Assert.That(ringBuffer[1].Value, Is.EqualTo(1L));
    }

    [Test]
    public void ShouldProcessOnlyOnceItHasBeenPublished()
    {
        var ringBuffer = RingBuffer<AtomicLong>.CreateMultiProducer(() => new AtomicLong(), 1024, new BlockingWaitStrategy());
        var pool = new WorkerPool<AtomicLong>(ringBuffer,
                                              Array.Empty<Sequence>(),
                                              new FatalExceptionHandler<AtomicLong>(),
                                              new AtomicLongWorkHandler(),
                                              new AtomicLongWorkHandler());

        pool.Start();

        ringBuffer.Next();
        ringBuffer.Next();

        Thread.Sleep(1000);

        Assert.That(ringBuffer[0].Value, Is.EqualTo(0L));
        Assert.That(ringBuffer[1].Value, Is.EqualTo(0L));
    }

    public class AtomicLong
    {
        private long _value;

        public long Value => _value;

        public void Increment()
        {
            Interlocked.Increment(ref _value);
        }
    }

    public class AtomicLongWorkHandler : IWorkHandler<AtomicLong>
    {
        public void OnEvent(AtomicLong evt)
        {
            evt.Increment();
        }
    }
}
