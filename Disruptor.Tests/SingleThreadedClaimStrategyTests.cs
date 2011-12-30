using System.Threading;
using Moq;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class SingleThreadedClaimStrategyTests
    {
        private const int BufferSize = 8;
        private IClaimStrategy _claimStrategy;

        [SetUp]
        public void SetUp()
        {
            _claimStrategy = new SingleThreadedClaimStrategy(BufferSize);
        }

        [Test]
        public void ShouldGetCorrectBufferSize()
        {
            Assert.AreEqual(BufferSize,_claimStrategy.BufferSize);
        }

        [Test]
        public void ShouldGetInitialSequence()
        {
            Assert.AreEqual(Sequencer.InitialCursorValue, _claimStrategy.Sequence);
        }

        [Test]
        public void ShouldClaimInitialSequence()
        {
            var dependentSequence = new Mock<Sequence>();

            Sequence[] dependentSequences = { dependentSequence.Object };
            const long expectedSequence = Sequencer.InitialCursorValue + 1L;

            Assert.AreEqual(expectedSequence, _claimStrategy.IncrementAndGet(dependentSequences));
            Assert.AreEqual(expectedSequence, _claimStrategy.Sequence);
        }

        [Test]
        public void ShouldClaimInitialBatchOfSequences()
        {
            var dependentSequence = new Mock<Sequence>();

            Sequence[] dependentSequences = { dependentSequence.Object };
            const int batchSize = 5;
            const long expectedSequence = Sequencer.InitialCursorValue + batchSize;

            Assert.AreEqual(expectedSequence, _claimStrategy.IncrementAndGet(batchSize, dependentSequences));
            Assert.AreEqual(expectedSequence, _claimStrategy.Sequence);
        }

        [Test]
        public void ShouldSetSequenceToValue()
        {
            var dependentSequence = new Mock<Sequence>();

            Sequence[] dependentSequences = { dependentSequence.Object };
            const int expectedSequence = 5;
            _claimStrategy.SetSequence(expectedSequence, dependentSequences);

            Assert.AreEqual(expectedSequence, _claimStrategy.Sequence);
        }

        [Test]
        public void ShouldHaveInitialAvailableCapacity()
        {
            var dependentSequence = new Mock<Sequence>();
            
            Sequence[] dependentSequences = { dependentSequence.Object };

            Assert.IsTrue(_claimStrategy.HasAvailableCapacity(1, dependentSequences));
        }

        [Test]
        public void ShouldNotHaveAvailableCapacityWhenBufferIsFull()
        {
            var dependentSequence = new Mock<Sequence>();

            dependentSequence.Setup(d => d.Value).Returns(Sequencer.InitialCursorValue);

            Sequence[] dependentSequences = { dependentSequence.Object };
            _claimStrategy.SetSequence(_claimStrategy.BufferSize - 1L, dependentSequences);

            Assert.IsFalse(_claimStrategy.HasAvailableCapacity(1, dependentSequences));
        }

        [Test]
        public void ShouldNotReturnNextClaimSequenceUntilBufferHasReserve()
        {
            var dependentSequence = new Sequence(Sequencer.InitialCursorValue);
            Sequence[] dependentSequences = { dependentSequence };
            _claimStrategy.SetSequence(_claimStrategy.BufferSize - 1L, dependentSequences);

            var done = new Volatile.Boolean(false);
            var beforeLatch = new ManualResetEvent(false);
            var afterLatch = new ManualResetEvent(false);

            new Thread(
                ()=>
                    {
                        beforeLatch.Set();

                        Assert.AreEqual(_claimStrategy.BufferSize, _claimStrategy.IncrementAndGet(dependentSequences));

                        done.WriteFullFence(true);
                        afterLatch.Set();
                    }).Start();

            beforeLatch.WaitOne();

            Thread.Sleep(100);
            Assert.IsFalse(done.ReadFullFence());

            dependentSequence.Value = (dependentSequence.Value + 1L);

            afterLatch.WaitOne();
            Assert.AreEqual(_claimStrategy.BufferSize, _claimStrategy.Sequence);
        }

        [Test]
        public void ShouldSerialisePublishingOnTheCursor()
        {
            var dependentSequence = new Sequence(Sequencer.InitialCursorValue);
            Sequence[] dependentSequences = { dependentSequence };

            long sequence = _claimStrategy.IncrementAndGet(dependentSequences);

            var cursor = new Sequence(Sequencer.InitialCursorValue);
            _claimStrategy.SerialisePublishing(sequence, cursor, 1);

            Assert.AreEqual(sequence, cursor.Value);
        }
    }
}