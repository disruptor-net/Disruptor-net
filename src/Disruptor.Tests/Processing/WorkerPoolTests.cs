using System;
using System.Diagnostics;
using System.Threading;
using Disruptor.Processing;
using Disruptor.Tests.Support;
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

    [Test]
    public void ShouldReportBacklog()
    {
        var ringBuffer = RingBuffer<BlockingEvent>.CreateMultiProducer(() => new BlockingEvent(), 1024);
        var pool = new WorkerPool<BlockingEvent>(ringBuffer, [], new FatalExceptionHandler<BlockingEvent>(), new BlockingWorkHandler(), new BlockingWorkHandler());

        pool.Start();

        var signal1 = new ManualResetEventSlim();
        var signal2 = new ManualResetEventSlim();
        var signal3 = new ManualResetEventSlim();

        using (var scope = ringBuffer.PublishEvent())
        {
            scope.Event().ResetEvent = signal1;
        }

        using (var scope = ringBuffer.PublishEvent())
        {
            scope.Event().ResetEvent = signal2;
        }

        using (var scope = ringBuffer.PublishEvent())
        {
            scope.Event().ResetEvent = signal3;
        }

        Assert.That(pool.HasBacklog());

        signal1.Set();
        Thread.Sleep(50);
        Assert.That(pool.HasBacklog());

        signal2.Set();
        Thread.Sleep(50);
        Assert.That(pool.HasBacklog());

        signal3.Set();

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < 500 && pool.HasBacklog())
        {
            Thread.Sleep(10);
        }

        Assert.That(!pool.HasBacklog());
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

    public class BlockingEvent
    {
        public ManualResetEventSlim? ResetEvent { get; set; }
    }

    public class BlockingWorkHandler : IWorkHandler<BlockingEvent>
    {
        public void OnEvent(BlockingEvent evt)
        {
            evt.ResetEvent?.Wait();
        }
    }
}
