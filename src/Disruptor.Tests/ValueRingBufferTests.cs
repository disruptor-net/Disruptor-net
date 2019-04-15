using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Tests.Support;
using NUnit.Framework;
using static Disruptor.Tests.ValueRingBufferEqualsConstraint;

namespace Disruptor.Tests
{
    [TestFixture]
    public class ValueRingBufferTests
    {
        private ValueRingBuffer<StubValueEvent> _ringBuffer;
        private ISequenceBarrier _sequenceBarrier;

        [SetUp]
        public void SetUp()
        {
            _ringBuffer = ValueRingBuffer<StubValueEvent>.CreateMultiProducer(() => new StubValueEvent(-1), 32);
            _sequenceBarrier = _ringBuffer.NewBarrier();
            _ringBuffer.AddGatingSequences(new NoOpEventProcessor<StubValueEvent>(_ringBuffer).Sequence);
        }

        [Test]
        public void ShouldClaimAndGet()
        {
            Assert.AreEqual(Sequence.InitialCursorValue, _ringBuffer.Cursor);

            var expectedEvent = new StubValueEvent(2701);

            var claimSequence = _ringBuffer.Next();
            ref var oldEvent = ref _ringBuffer[claimSequence];
            oldEvent.Copy(expectedEvent);
            _ringBuffer.Publish(claimSequence);

            var sequence = _sequenceBarrier.WaitFor(0);
            Assert.AreEqual(0, sequence);

            var evt = _ringBuffer[sequence];
            Assert.AreEqual(expectedEvent, evt);

            Assert.AreEqual(0L, _ringBuffer.Cursor);
        }

        [Test]
        public void ShouldClaimAndGetInSeparateThread()
        {
            var events = GetEvents(0, 0);

            var expectedEvent = new StubValueEvent(2701);

            var sequence = _ringBuffer.Next();
            ref var oldEvent = ref _ringBuffer[sequence];
            oldEvent.Copy(expectedEvent);

            using (var scope = _ringBuffer.PublishEvent())
            {
                scope.Data = expectedEvent;
            }

            Assert.AreEqual(expectedEvent, events.Result[0]);
        }

        [Test]
        public void ShouldClaimAndGetMultipleMessages()
        {
            var numEvents = _ringBuffer.BufferSize;
            for (var i = 0; i < numEvents; i++)
            {
                using (var scope = _ringBuffer.PublishEvent())
                {
                    scope.Data.Value = i;
                }
            }

            var expectedSequence = numEvents - 1;
            var available = _sequenceBarrier.WaitFor(expectedSequence);
            Assert.AreEqual(expectedSequence, available);

            for (var i = 0; i < numEvents; i++)
            {
                Assert.AreEqual(i, _ringBuffer[i].Value);
            }
        }

        [Test]
        public void ShouldWrap()
        {
            var numEvents = _ringBuffer.BufferSize;
            const int offset = 1000;
            for (var i = 0; i < numEvents + offset; i++)
            {
                using (var scope = _ringBuffer.PublishEvent())
                {
                    scope.Data.Value = i;
                }
            }

            var expectedSequence = numEvents + offset - 1;
            var available = _sequenceBarrier.WaitFor(expectedSequence);
            Assert.AreEqual(expectedSequence, available);

            for (var i = offset; i < numEvents + offset; i++)
            {
                Assert.AreEqual(i, _ringBuffer[i].Value);
            }
        }

        private Task<List<StubValueEvent>> GetEvents(long initial, long toWaitFor)
        {
            var barrier = new Barrier(2);
            var dependencyBarrier = _ringBuffer.NewBarrier();

            var testWaiter = new TestWaiter(barrier, dependencyBarrier, _ringBuffer, initial, toWaitFor);
            var task = Task.Factory.StartNew(() => testWaiter.Call());

            barrier.SignalAndWait();

            return task;
        }

        [Test]
        public void ShouldPreventWrapping()
        {
            var sequence = new Sequence();
            var ringBuffer = ValueRingBuffer<StubValueEvent>.CreateMultiProducer(() => new StubValueEvent(-1), 4);
            ringBuffer.AddGatingSequences(sequence);

            for (var i = 0; i <= 3; i++)
            {
                using (var scope = ringBuffer.PublishEvent())
                {
                    scope.Data.Value = i;
                    scope.Data.TestString = i.ToString();
                }
            }

            Assert.IsFalse(ringBuffer.TryNext(out _));
        }

        [Test]
        public void ShouldThrowExceptionIfBufferIsFull()
        {
            _ringBuffer.AddGatingSequences(new Sequence(_ringBuffer.BufferSize));

            try
            {
                for (var i = 0; i < _ringBuffer.BufferSize; i++)
                {
                    _ringBuffer.Publish(_ringBuffer.TryNext());
                }
            }
            catch (Exception)
            {
                throw new ApplicationException("Should not of thrown exception");
            }

            try
            {
                _ringBuffer.TryNext();
                throw new ApplicationException("Exception should have been thrown");
            }
            catch (InsufficientCapacityException)
            {
            }
        }

        [Test]
        public void ShouldPreventProducersOvertakingEventProcessorsWrapPoint()
        {
            const int ringBufferSize = 4;
            var mre = new ManualResetEvent(false);
            var producerComplete = false;
            var ringBuffer = new ValueRingBuffer<StubValueEvent>(() => new StubValueEvent(-1), ringBufferSize);
            var processor = new TestEventProcessor(ringBuffer.NewBarrier());
            ringBuffer.AddGatingSequences(processor.Sequence);

            var thread = new Thread(
                () =>
                {
                    for (var i = 0; i <= ringBufferSize; i++) // produce 5 events
                    {
                        var sequence = ringBuffer.Next();
                        ref var evt = ref ringBuffer[sequence];
                        evt.Value = i;
                        ringBuffer.Publish(sequence);

                        if (i == 3) // unblock main thread after 4th eventData published
                        {
                            mre.Set();
                        }
                    }

                    producerComplete = true;
                });

            thread.Start();

            mre.WaitOne();
            Assert.That(ringBuffer.Cursor, Is.EqualTo(ringBufferSize - 1));
            Assert.IsFalse(producerComplete);

            processor.Run();
            thread.Join();

            Assert.IsTrue(producerComplete);
        }

        [Test]
        public void ShouldPublishEvent()
        {
            var ringBuffer = ValueRingBuffer<long>.CreateSingleProducer(() => -1L, 4);

            using (var scope = ringBuffer.PublishEvent())
            {
                scope.Data = scope.Sequence;
            }

            Assert.IsTrue(ringBuffer.TryPublishEvent(out var s));
            using (s)
            {
                s.Data = s.Sequence;
            }

            Assert.That(ringBuffer, IsValueRingBufferWithEvents(0L, 1L));
        }

        [Test]
        public void ShouldPublishEvents()
        {
            var ringBuffer = ValueRingBuffer<long>.CreateSingleProducer(() => -1L, 4);

            using (var scope = ringBuffer.PublishEvents(2))
            {
                scope.Data(0) = scope.StartSequence;
                scope.Data(1) = scope.StartSequence + 1;
            }
            Assert.That(ringBuffer, IsValueRingBufferWithEvents(0L, 1L, -1, -1));

            Assert.IsTrue(ringBuffer.TryPublishEvents(2, out var s));
            using (s)
            {
                s.Data(0) = s.StartSequence;
                s.Data(1) = s.StartSequence + 1;
            }

            Assert.That(ringBuffer, IsValueRingBufferWithEvents(0L, 1L, 2L, 3L));
        }

        [Test]
        public void ShouldNotPublishEventsIfBatchIsLargerThanRingBuffer()
        {
            var ringBuffer = ValueRingBuffer<long>.CreateSingleProducer(() => -1L, 4);

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(5, out _));
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
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(0, out _));
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
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(-1, out _));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldAddAndRemoveSequences()
        {
            var ringBuffer = ValueRingBuffer<long>.CreateSingleProducer(() => -1L, 16);

            var sequenceThree = new Sequence(-1);
            var sequenceSeven = new Sequence(-1);
            ringBuffer.AddGatingSequences(sequenceThree, sequenceSeven);

            for (var i = 0; i < 10; i++)
            {
                ringBuffer.Publish(ringBuffer.Next());
            }

            sequenceThree.SetValue(3);
            sequenceSeven.SetValue(7);

            Assert.That(ringBuffer.GetMinimumGatingSequence(), Is.EqualTo(3L));
            Assert.IsTrue(ringBuffer.RemoveGatingSequence(sequenceThree));
            Assert.That(ringBuffer.GetMinimumGatingSequence(), Is.EqualTo(7L));
        }

        [Test]
        public void ShouldHandleResetToAndNotWrapUnnecessarilySingleProducer()
        {
            AssertHandleResetAndNotWrap(ValueRingBuffer<StubValueEvent>.CreateSingleProducer(StubValueEvent.EventFactory, 4));
        }

        [Test]
        public void ShouldHandleResetToAndNotWrapUnnecessarilyMultiProducer()
        {
            AssertHandleResetAndNotWrap(ValueRingBuffer<StubValueEvent>.CreateMultiProducer(StubValueEvent.EventFactory, 4));
        }

        private static void AssertHandleResetAndNotWrap(ValueRingBuffer<StubValueEvent> rb)
        {
            var sequence = new Sequence();
            rb.AddGatingSequences(sequence);

            for (var i = 0; i < 128; i++)
            {
                rb.Publish(rb.Next());
                sequence.IncrementAndGet();
            }

            Assert.That(rb.Cursor, Is.EqualTo(127L));

            rb.ResetTo(31);
            sequence.SetValue(31);

            for (var i = 0; i < 4; i++)
            {
                rb.Publish(rb.Next());
            }

            Assert.That(rb.HasAvailableCapacity(1), Is.EqualTo(false));
        }

        private Task<List<StubValueEvent>> GetMessages(long initial, long toWaitFor)
        {
            var cyclicBarrier = new Barrier(2);
            var sequenceBarrier = _ringBuffer.NewBarrier();

            var f = Task.Factory.StartNew(() => new TestWaiter(cyclicBarrier, sequenceBarrier, _ringBuffer, initial, toWaitFor).Call());

            cyclicBarrier.SignalAndWait();

            return f;
        }

        private static void AssertEmptyRingBuffer(ValueRingBuffer<long> ringBuffer)
        {
            Assert.That(ringBuffer[0], Is.EqualTo(-1));
            Assert.That(ringBuffer[1], Is.EqualTo(-1));
            Assert.That(ringBuffer[2], Is.EqualTo(-1));
            Assert.That(ringBuffer[3], Is.EqualTo(-1));
        }

        private class TestEventProcessor : IEventProcessor
        {
            private readonly ISequenceBarrier _sequenceBarrier;

            public TestEventProcessor(ISequenceBarrier sequenceBarrier)
            {
                _sequenceBarrier = sequenceBarrier;
            }

            public ISequence Sequence { get; } = new Sequence();

            public void Halt()
            {
                IsRunning = false;
            }

            public void Run()
            {
                IsRunning = true;
                _sequenceBarrier.WaitFor(0L);
                Sequence.SetValue(Sequence.Value + 1);
            }

            public bool IsRunning { get; private set; }
        }

        private class TestWaiter
        {
            private readonly Barrier _barrier;
            private readonly ISequenceBarrier _sequenceBarrier;
            private readonly long _initialSequence;
            private readonly long _toWaitForSequence;
            private readonly ValueRingBuffer<StubValueEvent> _ringBuffer;

            public TestWaiter(Barrier barrier, ISequenceBarrier sequenceBarrier, ValueRingBuffer<StubValueEvent> ringBuffer, long initialSequence, long toWaitForSequence)
            {
                _barrier = barrier;
                _sequenceBarrier = sequenceBarrier;
                _ringBuffer = ringBuffer;
                _initialSequence = initialSequence;
                _toWaitForSequence = toWaitForSequence;
            }

            public List<StubValueEvent> Call()
            {
                _barrier.SignalAndWait();
                _sequenceBarrier.WaitFor(_toWaitForSequence);

                var events = new List<StubValueEvent>();
                for (var l = _initialSequence; l <= _toWaitForSequence; l++)
                {
                    events.Add(_ringBuffer[l]);
                }

                return events;
            }
        }
    }
}
