using System;
using Disruptor.Dsl;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests;

public class ValueRingBufferTests : ValueRingBufferFixture<StubValueEvent>
{
    public ValueRingBufferTests()
        : base(x => CreateRingBuffer(x.size, x.producerType))
    {
    }

    private static IValueRingBuffer<StubValueEvent> CreateRingBuffer(int size, ProducerType producerType)
    {
        return new ValueRingBuffer<StubValueEvent>(() => new StubValueEvent(-1), SequencerFactory.Create(producerType, size));
    }

    [Test]
    public void ShouldPublishEvent()
    {
        var ringBuffer = ValueRingBuffer<long>.CreateSingleProducer(() => -1L, 4);

        using (var scope = ringBuffer.PublishEvent())
        {
            scope.Event() = scope.Sequence;
        }

        using (var scope = ringBuffer.TryPublishEvent())
        {
            Assert.That(scope.HasEvent);
            Assert.That(scope.TryGetEvent(out var e));
            e.Event() = e.Sequence;
        }

        Assert.That(ringBuffer, IsValueRingBuffer.WithEvents(0L, 1L));
    }

    [Test]
    public void ShouldPublishEvents()
    {
        var ringBuffer = ValueRingBuffer<long>.CreateSingleProducer(() => -1L, 4);

        using (var scope = ringBuffer.PublishEvents(2))
        {
            scope.Event(0) = scope.StartSequence;
            scope.Event(1) = scope.StartSequence + 1;
        }

        Assert.That(ringBuffer, IsValueRingBuffer.WithEvents(0L, 1L, -1, -1));

        using (var scope = ringBuffer.TryPublishEvents(2))
        {
            Assert.That(scope.HasEvents);
            Assert.That(scope.TryGetEvents(out var e));
            e.Event(0) = e.StartSequence;
            e.Event(1) = e.StartSequence + 1;
        }

        Assert.That(ringBuffer, IsValueRingBuffer.WithEvents(0L, 1L, 2L, 3L));
    }

    [Test]
    public void ShouldNotPublishEventsIfBatchIsLargerThanRingBuffer()
    {
        var ringBuffer = ValueRingBuffer<long>.CreateSingleProducer(() => -1L, 4);

        try
        {
            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(5));
        }
        finally
        {
            AssertEmptyRingBuffer(ringBuffer);
        }
    }

    [Test]
    public void ShouldNotPublishEventsWhenBatchSizeIs0()
    {
        var ringBuffer = ValueRingBuffer<long>.CreateSingleProducer(() => -1L, 4);

        try
        {
            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(0));
        }
        finally
        {
            AssertEmptyRingBuffer(ringBuffer);
        }
    }

    [Test]
    public void ShouldNotTryPublishEventsWhenBatchSizeIs0()
    {
        var ringBuffer = ValueRingBuffer<long>.CreateSingleProducer(() => -1L, 4);

        try
        {
            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(0));
        }
        finally
        {
            AssertEmptyRingBuffer(ringBuffer);
        }
    }

    [Test]
    public void ShouldNotPublishEventsWhenBatchSizeIsNegative()
    {
        var ringBuffer = ValueRingBuffer<long>.CreateSingleProducer(() => -1L, 4);

        try
        {
            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(-1));
        }
        finally
        {
            AssertEmptyRingBuffer(ringBuffer);
        }
    }

    [Test]
    public void ShouldNotTryPublishEventsWhenBatchSizeIsNegative()
    {
        var ringBuffer = ValueRingBuffer<long>.CreateSingleProducer(() => -1L, 4);

        try
        {
            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(-1));
        }
        finally
        {
            AssertEmptyRingBuffer(ringBuffer);
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(31)]
    [TestCase(32)]
    [TestCase(int.MaxValue)]
    [TestCase(int.MaxValue +1L)]
    public void ShouldGetEventFromSequence(long sequence)
    {
        var index = 0;
        var ringBuffer = new ValueRingBuffer<StubValueEvent>(() => new StubValueEvent(index++), 32);

        ref var evt = ref ringBuffer[sequence];

        var expectedIndex = sequence % 32;
        Assert.That(evt.Value, Is.EqualTo(expectedIndex));
    }
}
