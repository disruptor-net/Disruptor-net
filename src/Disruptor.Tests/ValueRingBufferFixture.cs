using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.Tests.Support;
using NUnit.Framework;

#pragma warning disable 618,612

namespace Disruptor.Tests
{
    [TestFixture]
    public abstract class ValueRingBufferFixture<T>
        where T : struct, IStubEvent
    {
        private IValueRingBuffer<T> _ringBuffer;
        private ISequenceBarrier _sequenceBarrier;

        [SetUp]
        public virtual void SetUp()
        {
            _ringBuffer = CreateRingBuffer(32, ProducerType.Multi);
            _sequenceBarrier = _ringBuffer.NewBarrier();
            _ringBuffer.AddGatingSequences(new NoOpEventProcessor<T>(_ringBuffer).Sequence);
        }

        [TearDown]
        public virtual void Teardown()
        {
        }

        protected abstract IValueRingBuffer<T> CreateRingBuffer(int size, ProducerType producerType);

        [Test]
        public void ShouldClaimAndGet()
        {
            Assert.AreEqual(Sequence.InitialCursorValue, _ringBuffer.Cursor);

            var expectedEvent = new T { Value = 2701 };

            var claimSequence = _ringBuffer.Next();
            _ringBuffer[claimSequence] = expectedEvent;
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

            var expectedEvent = new T { Value = 2701 };

            var sequence = _ringBuffer.Next();
            _ringBuffer[sequence] = expectedEvent;
            _ringBuffer.Publish(sequence);

            Assert.AreEqual(expectedEvent, events.Result[0]);
        }

        [Test]
        public void ShouldClaimAndGetMultipleMessages()
        {
            var numEvents = _ringBuffer.BufferSize;
            for (var i = 0; i < numEvents; i++)
            {
                var sequence = _ringBuffer.Next();
                _ringBuffer[sequence].Value = i;
                _ringBuffer.Publish(sequence);
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
                var sequence = _ringBuffer.Next();
                _ringBuffer[sequence].Value = i;
                _ringBuffer.Publish(sequence);
            }

            var expectedSequence = numEvents + offset - 1;
            var available = _sequenceBarrier.WaitFor(expectedSequence);
            Assert.AreEqual(expectedSequence, available);

            for (var i = offset; i < numEvents + offset; i++)
            {
                Assert.AreEqual(i, _ringBuffer[i].Value);
            }
        }

        private Task<List<T>> GetEvents(long initial, long toWaitFor)
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
            var ringBuffer = CreateRingBuffer(4, ProducerType.Multi);
            ringBuffer.AddGatingSequences(sequence);

            for (var i = 0; i <= 3; i++)
            {
                var l = ringBuffer.Next();
                ringBuffer.Publish(l);
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
            var ringBuffer = CreateRingBuffer(ringBufferSize, ProducerType.Multi);
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
        public void ShouldAddAndRemoveSequences()
        {
            var ringBuffer = CreateRingBuffer(16, ProducerType.Single);

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
            AssertHandleResetAndNotWrap(CreateRingBuffer(4, ProducerType.Single));
        }

        [Test]
        public void ShouldHandleResetToAndNotWrapUnnecessarilyMultiProducer()
        {
            AssertHandleResetAndNotWrap(CreateRingBuffer(4, ProducerType.Multi));
        }

        private static void AssertHandleResetAndNotWrap(IValueRingBuffer<T> rb)
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

        protected static void AssertEmptyRingBuffer(IValueRingBuffer<long> ringBuffer)
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
            private readonly IValueRingBuffer<T> _ringBuffer;

            public TestWaiter(Barrier barrier, ISequenceBarrier sequenceBarrier, IValueRingBuffer<T> ringBuffer, long initialSequence, long toWaitForSequence)
            {
                _barrier = barrier;
                _sequenceBarrier = sequenceBarrier;
                _ringBuffer = ringBuffer;
                _initialSequence = initialSequence;
                _toWaitForSequence = toWaitForSequence;
            }

            public List<T> Call()
            {
                _barrier.SignalAndWait();
                _sequenceBarrier.WaitFor(_toWaitForSequence);

                var events = new List<T>();
                for (var l = _initialSequence; l <= _toWaitForSequence; l++)
                {
                    events.Add(_ringBuffer[l]);
                }

                return events;
            }
        }
    }
}
