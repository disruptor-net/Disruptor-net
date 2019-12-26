using System;
using Disruptor.Dsl;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests
{
    public class ValueRingBufferTests : ValueRingBufferFixture<StubValueEvent>
    {
        protected override IValueRingBuffer<StubValueEvent> CreateRingBuffer(int size, ProducerType producerType)
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
                Assert.IsTrue(scope.HasEvent);
                Assert.IsTrue(scope.TryGetEvent(out var e));
                e.Event() = e.Sequence;
            }

            Assert.That(ringBuffer, ValueRingBufferEqualsConstraint.IsValueRingBufferWithEvents(0L, 1L));
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

            Assert.That(ringBuffer, ValueRingBufferEqualsConstraint.IsValueRingBufferWithEvents(0L, 1L, -1, -1));

            using (var scope = ringBuffer.TryPublishEvents(2))
            {
                Assert.IsTrue(scope.HasEvents);
                Assert.IsTrue(scope.TryGetEvents(out var e));
                e.Event(0) = e.StartSequence;
                e.Event(1) = e.StartSequence + 1;
            }

            Assert.That(ringBuffer, ValueRingBufferEqualsConstraint.IsValueRingBufferWithEvents(0L, 1L, 2L, 3L));
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
    }
}
