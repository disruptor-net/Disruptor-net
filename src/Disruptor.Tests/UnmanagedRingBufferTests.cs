using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Disruptor.Dsl;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests
{
    public class UnmanagedRingBufferTests : ValueRingBufferFixture<StubUnmanagedEvent>
    {
        private List<UnmanagedRingBufferMemory> _memoryList;

        public override void SetUp()
        {
            _memoryList = new List<UnmanagedRingBufferMemory>();

            base.SetUp();
        }

        public override void Teardown()
        {
            base.Teardown();

            foreach (var memory in _memoryList)
            {
                memory.Dispose();
            }
        }

        protected override IValueRingBuffer<StubUnmanagedEvent> CreateRingBuffer(int size, ProducerType producerType)
        {
            var memory = UnmanagedRingBufferMemory.Allocate(size, () => new StubUnmanagedEvent(-1));
            _memoryList.Add(memory);

            return new UnmanagedRingBuffer<StubUnmanagedEvent>(memory, producerType, new BlockingWaitStrategy());
        }

        private UnmanagedRingBuffer<T> CreateSingleProducer<T>(Func<T> eventFactory, int size)
            where T : unmanaged
        {
            var memory = UnmanagedRingBufferMemory.Allocate(size, eventFactory);
            _memoryList.Add(memory);

            return new UnmanagedRingBuffer<T>(memory, ProducerType.Single, new BlockingWaitStrategy());
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ShouldNotCreateRingBufferWithInvalidEventSize(int eventSize)
        {
            using (var memory = UnmanagedRingBufferMemory.Allocate(1, 1))
            {
                Assert.Throws<ArgumentException>(() => GC.KeepAlive(new UnmanagedRingBuffer<StubUnmanagedEvent>(memory.PointerToFirstEvent, eventSize, new SingleProducerSequencer(1))));
            }
        }

        [Test]
        public void ShouldPublishEvent()
        {
            var ringBuffer = CreateSingleProducer(() => -1L, 4);

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
            var ringBuffer = CreateSingleProducer(() => -1L, 4);

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
            var ringBuffer = CreateSingleProducer(() => -1L, 4);

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
            var ringBuffer = CreateSingleProducer(() => -1L, 4);

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
            var ringBuffer = CreateSingleProducer(() => -1L, 4);

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
            var ringBuffer = CreateSingleProducer(() => -1L, 4);

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
            var ringBuffer = CreateSingleProducer(() => -1L, 4);

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
            using var memory = UnmanagedRingBufferMemory.Allocate(32, () => new StubUnmanagedEvent(index++));
            var ringBuffer = new UnmanagedRingBuffer<StubUnmanagedEvent>(memory, ProducerType.Single, new BlockingWaitStrategy());

            ref var evt = ref ringBuffer[sequence];

            var expectedIndex = sequence % 32;
            Assert.That(evt.Value, Is.EqualTo(expectedIndex));
        }
    }
}
