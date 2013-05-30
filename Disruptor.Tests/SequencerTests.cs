using System;
using System.Threading;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class SequencerTests
    {
        private const int BufferSize = 4;
        private Sequencer _sequencer;
        private Sequence _gatingSequence;

        [SetUp]
        public void SetUp()
        {
            _gatingSequence = new Sequence(Sequencer.InitialCursorValue);

            _sequencer = new Sequencer(new SingleThreadedClaimStrategy(BufferSize), new SleepingWaitStrategy());
            _sequencer.SetGatingSequences(_gatingSequence);
        }

        [Test]
        public void ShouldStartWithInitialValue()
        {
            Assert.AreEqual(Sequencer.InitialCursorValue, _sequencer.Cursor);
        }

        [Test]
        public void ShouldGetPublishFirstSequence()
        {
            long sequence = _sequencer.Next();
            Assert.AreEqual(Sequencer.InitialCursorValue, _sequencer.Cursor);
            Assert.AreEqual(sequence, 0L);

            _sequencer.Publish(sequence);
            Assert.AreEqual(sequence, _sequencer.Cursor);
        }

        [Test]
        public void ShouldIndicateAvailableCapacity()
        {
            Assert.IsTrue(_sequencer.HasAvailableCapacity(1));
        }

        [Test]
        public void ShouldIndicateNoAvailableCapacity()
        {
            FillBuffer();

            Assert.IsFalse(_sequencer.HasAvailableCapacity(1));
        }

        [Test]
        public void ShouldForceClaimSequence()
        {
            const long claimSequence = 3L;

            long sequence = _sequencer.Claim(claimSequence);
            Assert.AreEqual(Sequencer.InitialCursorValue, _sequencer.Cursor);
            Assert.AreEqual(sequence, claimSequence);

            _sequencer.ForcePublish(sequence);
            Assert.AreEqual(claimSequence, _sequencer.Cursor);
        }

        [Test]
        public void ShouldPublishSequenceBatch()
        {
            const int batchSize = 3;
            var batchDescriptor = new BatchDescriptor(batchSize);

            batchDescriptor = _sequencer.Next(batchDescriptor);
            Assert.AreEqual(Sequencer.InitialCursorValue, _sequencer.Cursor);
            Assert.AreEqual(batchDescriptor.End, Sequencer.InitialCursorValue + batchSize);
            Assert.AreEqual(batchDescriptor.Size, batchSize);

            _sequencer.Publish(batchDescriptor);
            Assert.AreEqual(_sequencer.Cursor, Sequencer.InitialCursorValue + batchSize);
        }

        [Test]
        public void ShouldWaitOnSequence()
        {
            var barrier = _sequencer.NewBarrier();
            long sequence = _sequencer.Next();
            _sequencer.Publish(sequence);

            Assert.AreEqual(sequence, barrier.WaitFor(sequence));
        }

        [Test]
        public void ShouldWaitOnSequenceShowingBatchingEffect()
        {
            var barrier = _sequencer.NewBarrier();
            _sequencer.Publish(_sequencer.Next());
            _sequencer.Publish(_sequencer.Next());

            long sequence = _sequencer.Next();
            _sequencer.Publish(sequence);

            Assert.AreEqual(sequence, barrier.WaitFor(Sequencer.InitialCursorValue + 1L));
        }

        [Test]
        public void ShouldSignalWaitingProcessorWhenSequenceIsPublished()
        {
            var barrier = _sequencer.NewBarrier();
            var waitingLatch = new ManualResetEvent(false);
            var doneLatch = new ManualResetEvent(false);
            const long expectedSequence = Sequencer.InitialCursorValue + 1L;

            new Thread(
                () =>
                    {
                        waitingLatch.Set();
                        Assert.AreEqual(expectedSequence, barrier.WaitFor(expectedSequence));

                        _gatingSequence.Value = expectedSequence;
                        doneLatch.Set();
                    }).Start();

            waitingLatch.WaitOne();
            Assert.AreEqual(_gatingSequence.Value, Sequencer.InitialCursorValue);

            _sequencer.Publish(_sequencer.Next());

            doneLatch.WaitOne();
            Assert.AreEqual(_gatingSequence.Value, expectedSequence);
        }

        [Test]
        public void ShouldHoldUpPublisherWhenBufferIsFull()
        {
            FillBuffer();

            var waitingLatch = new ManualResetEvent(false);
            var doneLatch = new ManualResetEvent(false);

            long expectedFullSequence = Sequencer.InitialCursorValue + _sequencer.BufferSize;
            Assert.AreEqual(_sequencer.Cursor, expectedFullSequence);

            new Thread(
                ()=>
                    {
                        waitingLatch.Set();

                        _sequencer.Publish(_sequencer.Next());

                        doneLatch.Set();
                    }).Start();

            waitingLatch.WaitOne();
            Assert.AreEqual(_sequencer.Cursor, expectedFullSequence);

            _gatingSequence.Value = Sequencer.InitialCursorValue + 1L;

            doneLatch.WaitOne();
            Assert.AreEqual(_sequencer.Cursor, expectedFullSequence + 1L);
        }

        [Test]
        [ExpectedException(typeof(InsufficientCapacityException))]
        public void ShouldThrowInsufficientCapacityExceptionWhenSequencerIsFull()
        {
            _sequencer.TryNext(5);
        }

        [Test]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void ShouldRejectAvailableCapcityLessThanOne()
        {
            _sequencer.TryNext(0);
        }

        private void FillBuffer()
        {
            for (int i = 0; i < BufferSize; i++)
            {
                long sequence = _sequencer.Next();
                _sequencer.Publish(sequence);
            }
        }
    }
}