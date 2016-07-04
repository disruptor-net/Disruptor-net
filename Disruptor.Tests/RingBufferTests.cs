using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Tests.Support;
using NUnit.Framework;
using Gen = System.Collections.Generic;

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
            _ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(-1), 32);
            _sequenceBarrier = _ringBuffer.NewBarrier();
            _ringBuffer.SetGatingSequences(new NoOpEventProcessor(_ringBuffer).Sequence);
        }

        [Test]
        public void ShouldClaimAndGet()
        {
            Assert.AreEqual(Sequencer.InitialCursorValue, _ringBuffer.Cursor);

            var expectedEvent = new StubEvent(2701);

            var claimSequence = _ringBuffer.Next();
            var oldEvent = _ringBuffer[claimSequence];
            oldEvent.Copy(expectedEvent);
            _ringBuffer.Publish(claimSequence);

            long sequence = _sequenceBarrier.WaitFor(0);
            Assert.AreEqual(0, sequence);

            StubEvent evt = _ringBuffer[sequence];
            Assert.AreEqual(expectedEvent, evt);

            Assert.AreEqual(0L, _ringBuffer.Cursor);
        }

        [Test]
        public void ShouldClaimAndGetWithTimeout()
        {
            Assert.AreEqual(Sequencer.InitialCursorValue, _ringBuffer.Cursor);

            var expectedEvent = new StubEvent(2701);

            long claimSequence = _ringBuffer.Next();
            StubEvent oldEvent = _ringBuffer[claimSequence];
            oldEvent.Copy(expectedEvent);
            _ringBuffer.Publish(claimSequence);

            long sequence = _sequenceBarrier.WaitFor(0, TimeSpan.FromMilliseconds(5));
            Assert.AreEqual(0, sequence);

            var evt = _ringBuffer[sequence];
            Assert.AreEqual(expectedEvent, evt);

            Assert.AreEqual(0L, _ringBuffer.Cursor);
        }

        [Test]
        public void ShouldGetWithTimeout()
        {
            long sequence = _sequenceBarrier.WaitFor(0, TimeSpan.FromMilliseconds(5));
            Assert.AreEqual(Sequencer.InitialCursorValue, sequence);
        }

        [Test]
        public void ShouldClaimAndGetInSeparateThread()
        {
            var events = GetEvents(0, 0);

            var expectedEvent = new StubEvent(2701);

            var sequence = _ringBuffer.Next();
            StubEvent oldEvent = _ringBuffer[sequence];
            oldEvent.Copy(expectedEvent);
            _ringBuffer.Publish(sequence);

            Assert.AreEqual(expectedEvent, events.Result[0]);
        }

        [Test]
        public void ShouldClaimAndGetMultipleEvents()
        {
            var numEvents = _ringBuffer.BufferSize;
            for (var i = 0; i < numEvents; i++)
            {
                var sequence = _ringBuffer.Next();
                StubEvent evt = _ringBuffer[sequence];
                evt.Value = i;
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
                StubEvent evt = _ringBuffer[sequence];
                evt.Value = i;
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

        [Test]
        public void ShouldSetAtSpecificSequence()
        {
            const long expectedSequence = 5;

            _ringBuffer.Claim(expectedSequence);
            StubEvent expectedEvent = _ringBuffer[expectedSequence];
            expectedEvent.Value = (int) expectedSequence;
            _ringBuffer.ForcePublish(expectedSequence);

            long sequence = _sequenceBarrier.WaitFor(expectedSequence);
            Assert.AreEqual(expectedSequence, sequence);

            StubEvent evt = _ringBuffer[sequence];
            Assert.AreEqual(expectedEvent, evt);

            Assert.AreEqual(expectedSequence, _ringBuffer.Cursor);
        }

        private Task<Gen.List<StubEvent>> GetEvents(long initial, long toWaitFor)
        {
            var barrier = new Barrier(2);
            var dependencyBarrier = _ringBuffer.NewBarrier();

            var testWaiter = new TestWaiter(barrier, dependencyBarrier, _ringBuffer, initial, toWaitFor);
            var task = Task.Factory.StartNew(() => testWaiter.Call());

            barrier.SignalAndWait();

            return task;
        }

        [Test]
        public void ShouldPreventProducersOvertakingEventProcessorsWrapPoint()
        {
            const int ringBufferSize = 4;
            var mre = new ManualResetEvent(false);
            var producerComplete = false;
            var ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(-1), ringBufferSize);
            var processor = new TestEventProcessor(ringBuffer.NewBarrier());
            ringBuffer.SetGatingSequences(processor.Sequence);

            var thread = new Thread(
                () =>
                    {
                        for (int i = 0; i <= ringBufferSize; i++) // produce 5 events
                        {
                            var sequence = ringBuffer.Next();
                            StubEvent evt = ringBuffer[sequence];
                            evt.Value = i;
                            ringBuffer.Publish(sequence);

                            if (i == 3) // unblock main thread after 4th event published
                            {
                                mre.Set();
                            }
                        }

                        producerComplete = true;
                    });

            thread.Start();

            mre.WaitOne();
            Assert.AreEqual(ringBufferSize - 1, ringBuffer.Cursor);
            Assert.IsFalse(producerComplete);

            processor.Run();
            thread.Join();

            Assert.IsTrue(producerComplete);
        }

        private class TestEventProcessor : IEventProcessor
        {
            private readonly ISequenceBarrier _sequenceBarrier;
            private readonly Sequence _sequence = new Sequence(Sequencer.InitialCursorValue);

            public TestEventProcessor(ISequenceBarrier sequenceBarrier)
            {
                _sequenceBarrier = sequenceBarrier;
            }

            public Sequence Sequence
            {
                get { return _sequence; }
            }

            public void Halt()
            {
                IsRunning = false;
            }

            public void Run()
            {
                IsRunning = true;
                _sequenceBarrier.WaitFor(0L);
                _sequence.Value += 1;
            }

            public bool IsRunning { get; private set; }
        }
    }
}