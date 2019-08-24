using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Tests.Support;
using NUnit.Framework;
using static Disruptor.Tests.RingBufferEqualsConstraint;

#pragma warning disable 618,612

namespace Disruptor.Tests
{
    [TestFixture]
    public partial class RingBufferTests
    {
        private RingBuffer<StubEvent> _ringBuffer;
        private ISequenceBarrier _sequenceBarrier;

        [SetUp]
        public void SetUp()
        {
            _ringBuffer = RingBuffer<StubEvent>.CreateMultiProducer(() => new StubEvent(-1), 32);
            _sequenceBarrier = _ringBuffer.NewBarrier();
            _ringBuffer.AddGatingSequences(new NoOpEventProcessor<StubEvent>(_ringBuffer).Sequence);
        }

        [Test]
        public void ShouldClaimAndGet()
        {
            Assert.AreEqual(Sequence.InitialCursorValue, _ringBuffer.Cursor);

            var expectedEvent = new StubEvent(2701);

            var claimSequence = _ringBuffer.Next();
            var oldEvent = _ringBuffer[claimSequence];
            oldEvent.Copy(expectedEvent);
            _ringBuffer.Publish(claimSequence);

            var sequence = _sequenceBarrier.WaitFor(0);
            Assert.AreEqual(0, sequence);

            var evt = _ringBuffer[sequence];
            Assert.AreEqual(expectedEvent, evt);

            Assert.AreEqual(0L, _ringBuffer.Cursor);
        }

        [Test]
        public void ShouldNotClaimMoreThanCapacity()
        {
            Assert.Throws<ArgumentException>(() => _ringBuffer.Next(_ringBuffer.BufferSize + 1));
            Assert.Throws<ArgumentException>(() => _ringBuffer.TryNext(_ringBuffer.BufferSize + 1, out _));
        }

        [Test]
        public void ShouldClaimAndGetInSeparateThread()
        {
            var events = GetEvents(0, 0);

            var expectedEvent = new StubEvent(2701);

            var sequence = _ringBuffer.Next();
            var oldEvent = _ringBuffer[sequence];
            oldEvent.Copy(expectedEvent);

            using (var scope = _ringBuffer.PublishEvent())
            {
                scope.Event().Copy(expectedEvent);
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
                    scope.Event().Value = i;
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
                    scope.Event().Value = i;
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

        private Task<List<StubEvent>> GetEvents(long initial, long toWaitFor)
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
            var sequence = new Sequence(Sequence.InitialCursorValue);
            var ringBuffer = RingBuffer<StubEvent>.CreateMultiProducer(() => new StubEvent(-1), 4);
            ringBuffer.AddGatingSequences(sequence);

            for (var i = 0; i <= 3; i++)
            {
                using (var scope = ringBuffer.PublishEvent())
                {
                    scope.Event().Value = i;
                    scope.Event().TestString = i.ToString();
                }
            }

            Assert.IsFalse(ringBuffer.TryNext(out _));
        }

        [Test]
        public void ShouldReturnFalseIfBufferIsFull()
        {
            _ringBuffer.AddGatingSequences(new Sequence(_ringBuffer.BufferSize));

            for (var i = 0; i < _ringBuffer.BufferSize; i++)
            {
                var succeeded = _ringBuffer.TryNext(out var n);
                Assert.IsTrue(succeeded);

                _ringBuffer.Publish(n);
            }

            Assert.IsFalse(_ringBuffer.TryNext(out _));
        }

        [Test]
        public void ShouldPreventProducersOvertakingEventProcessorsWrapPoint()
        {
            const int ringBufferSize = 4;
            var mre = new ManualResetEvent(false);
            var producerComplete = false;
            var ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(-1), ringBufferSize);
            var processor = new TestEventProcessor(ringBuffer.NewBarrier());
            ringBuffer.AddGatingSequences(processor.Sequence);

            var thread = new Thread(
                () =>
                {
                    for (var i = 0; i <= ringBufferSize; i++) // produce 5 events
                    {
                        var sequence = ringBuffer.Next();
                        var evt = ringBuffer[sequence];
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
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);

            using (var scope = ringBuffer.PublishEvent())
            {
                scope.Event()[0] = scope.Sequence;
            }
            using (var scope = ringBuffer.TryPublishEvent())
            {
                Assert.IsTrue(scope.HasEvent);
                Assert.IsTrue(scope.TryGetEvent(out var e));
                e.Event()[0] = e.Sequence;
            }

            Assert.That(ringBuffer, IsRingBufferWithEvents(0L, 1L));
        }

        [Test]
        public void ShouldPublishEvents()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);

            using (var scope = ringBuffer.PublishEvents(2))
            {
                scope.Event(0)[0] = scope.StartSequence;
                scope.Event(1)[0] = scope.StartSequence + 1;
            }
            Assert.That(ringBuffer, IsRingBufferWithEvents(0L, 1L, null, null));

            using (var scope = ringBuffer.TryPublishEvents(2))
            {
                Assert.IsTrue(scope.HasEvents);
                Assert.IsTrue(scope.TryGetEvents(out var e));
                e.Event(0)[0] = e.StartSequence;
                e.Event(1)[0] = e.StartSequence + 1;
            }

            Assert.That(ringBuffer, IsRingBufferWithEvents(0L, 1L, 2L, 3L));
        }

        [Test]
        public void ShouldNotPublishEventsIfBatchIsLargerThanRingBuffer()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);

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
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);

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
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);

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
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);

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
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(-1));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldAddAndRemoveSequences()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 16);

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
            AssertHandleResetAndNotWrap(RingBuffer<StubEvent>.CreateSingleProducer(StubEvent.EventFactory, 4));
        }

        [Test]
        public void ShouldHandleResetToAndNotWrapUnnecessarilyMultiProducer()
        {
            AssertHandleResetAndNotWrap(RingBuffer<StubEvent>.CreateMultiProducer(StubEvent.EventFactory, 4));
        }

        private static void AssertHandleResetAndNotWrap(RingBuffer<StubEvent> rb)
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

        private static void AssertEmptyRingBuffer(RingBuffer<object[]> ringBuffer)
        {
            Assert.That(ringBuffer[0][0], Is.EqualTo(null));
            Assert.That(ringBuffer[1][0], Is.EqualTo(null));
            Assert.That(ringBuffer[2][0], Is.EqualTo(null));
            Assert.That(ringBuffer[3][0], Is.EqualTo(null));
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
            private readonly RingBuffer<StubEvent> _ringBuffer;

            public TestWaiter(Barrier barrier, ISequenceBarrier sequenceBarrier, RingBuffer<StubEvent> ringBuffer, long initialSequence, long toWaitForSequence)
            {
                _barrier = barrier;
                _sequenceBarrier = sequenceBarrier;
                _ringBuffer = ringBuffer;
                _initialSequence = initialSequence;
                _toWaitForSequence = toWaitForSequence;
            }

            public List<StubEvent> Call()
            {
                _barrier.SignalAndWait();
                _sequenceBarrier.WaitFor(_toWaitForSequence);

                var events = new List<StubEvent>();
                for (var l = _initialSequence; l <= _toWaitForSequence; l++)
                {
                    events.Add(_ringBuffer[l]);
                }

                return events;
            }
        }
    }
}
