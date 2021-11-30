using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;
using Disruptor.Tests.Support;
using NUnit.Framework;
using static Disruptor.Tests.RingBufferEqualsConstraint;

#pragma warning disable 618,612

namespace Disruptor.Tests
{
    [TestFixture]
    public class RingBufferTests
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

            var waitResult = _sequenceBarrier.WaitFor(0);
            Assert.AreEqual(new SequenceWaitResult(0), waitResult);

            var evt = _ringBuffer[waitResult.UnsafeAvailableSequence];
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
            var waitResult = _sequenceBarrier.WaitFor(expectedSequence);
            Assert.AreEqual(new SequenceWaitResult(expectedSequence), waitResult);

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
            var waitResult = _sequenceBarrier.WaitFor(expectedSequence);
            Assert.AreEqual(new SequenceWaitResult(expectedSequence), waitResult);

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
            var task = Task.Run(() => testWaiter.Call());

            barrier.SignalAndWait();

            return task;
        }

        [Test]
        public void ShouldPreventWrapping()
        {
            var sequence = new Sequence();
            var ringBuffer = RingBuffer<StubEvent>.CreateMultiProducer(() => new StubEvent(-1), 4);
            ringBuffer.AddGatingSequences(sequence);

            for (var i = 0; i <= 3; i++)
            {
                ringBuffer.PublishEvent().Dispose();
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
            var ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(-1), ringBufferSize);
            var processor = new TestEventProcessor(ringBuffer.NewBarrier());
            ringBuffer.AddGatingSequences(processor.Sequence);

            var task = Task.Run(() =>
            {
                // Attempt to put in enough events to wrap around the ring buffer
                for (var i = 0; i < ringBufferSize + 1; i++)
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
            });

            mre.WaitOne();

            // Publisher should not be complete, blocked at RingBuffer.Next
            Assert.IsFalse(task.IsCompleted);

            // Run the processor, freeing up entries in the ring buffer for the producer to continue and "complete"
            processor.Run();

            // Check producer completes
            Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(1)));
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

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(31)]
        [TestCase(32)]
        [TestCase(int.MaxValue)]
        [TestCase(int.MaxValue +1L)]
        public void ShouldGetEventFromSequence(long sequence)
        {
            var index = 0;
            var ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(index++), 32);

            var evt = ringBuffer[sequence];

            var expectedIndex = sequence % 32;
            Assert.That(evt.Value, Is.EqualTo(expectedIndex));
        }

#if NETCOREAPP
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(31)]
        [TestCase(32)]
        [TestCase(int.MaxValue)]
        [TestCase(int.MaxValue +1L)]
        public void ShouldGetEventSpanFromSequence_1(long sequence)
        {
            var index = 0;
            var ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(index++), 32);

            var span = ringBuffer[sequence, sequence];

            Assert.That(span.Length, Is.EqualTo(1));

            var expectedIndex = sequence % 32;
            Assert.That(span[0].Value, Is.EqualTo(expectedIndex));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(31)]
        [TestCase(32)]
        [TestCase(int.MaxValue)]
        [TestCase(int.MaxValue +1L)]
        public void ShouldGetEventSpanFromSequence_2(long sequence)
        {
            var init = 0;
            var ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(init++), 32);

            var span = ringBuffer[sequence, sequence + 31];

            var expectedStartIndex = sequence % 32;
            var expectedEndIndex = 31;

            Assert.That(span.Length, Is.EqualTo(1 + expectedEndIndex - expectedStartIndex));

            for (var index = 0; index < span.Length; index++)
            {
                Assert.That(span[index].Value, Is.EqualTo(expectedStartIndex + index));
            }
        }
#endif

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
