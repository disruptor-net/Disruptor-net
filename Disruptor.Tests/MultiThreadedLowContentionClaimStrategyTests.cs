using System.Collections.Concurrent;
using System.Threading;
using Moq;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class MultiThreadedLowContentionClaimStrategyTests
    {
        private const int BufferSize = 8;
        private IClaimStrategy _claimStrategy;

        [SetUp]
        public void SetUp()
        {
            _claimStrategy = new MultiThreadedLowContentionClaimStrategy(BufferSize);
        }

        [Test]
        public void ShouldGetCorrectBufferSize()
        {
            Assert.AreEqual(BufferSize, _claimStrategy.BufferSize);
        }

        [Test]
        public void ShouldGetInitialSequence()
        {
            Assert.AreEqual(Sequence.InitialCursorValue, _claimStrategy.Sequence);
        }

        [Test]
        public void ShouldClaimInitialSequence()
        {
            var dependentSequence = new Mock<Sequence>();

            var dependentSequences = new[] {dependentSequence.Object};
            const long expectedSequence = Sequence.InitialCursorValue + 1L;

            Assert.AreEqual(expectedSequence, _claimStrategy.IncrementAndGet(dependentSequences));
            Assert.AreEqual(expectedSequence, _claimStrategy.Sequence);
        }

        [Test]
        public void ShouldClaimInitialBatchOfSequences()
        {
            var dependentSequence = new Mock<Sequence>();

            var dependentSequences = new[] { dependentSequence.Object };
            const int batchSize = 5;
            const long expectedSequence = Sequence.InitialCursorValue + batchSize;

            Assert.AreEqual(expectedSequence, _claimStrategy.IncrementAndGet(batchSize, dependentSequences));
            Assert.AreEqual(expectedSequence, _claimStrategy.Sequence);
        }

        [Test]
        public void ShouldSetSequenceToValue()
        {
            var dependentSequence = new Mock<Sequence>();

            var dependentSequences = new[] { dependentSequence.Object };
            const long expectedSequence = 5L;
            _claimStrategy.SetSequence(expectedSequence, dependentSequences);

            Assert.AreEqual(expectedSequence, _claimStrategy.Sequence);
        }

        [Test]
        public void ShouldHaveInitialAvailableCapacity()
        {
            var dependentSequence = new Mock<Sequence>();

            var dependentSequences = new[] { dependentSequence.Object };

            Assert.IsTrue(_claimStrategy.HasAvailableCapacity(1, dependentSequences));
        }

        [Test]
        public void ShouldNotHaveAvailableCpapcityWhenBufferIsFull()
        {
            var dependentSequence = new Mock<Sequence>();

            dependentSequence.SetupGet(s => s.Value)
                             .Returns(Sequence.InitialCursorValue);

            var dependentSequences = new[] { dependentSequence.Object };
            _claimStrategy.SetSequence(BufferSize - 1L, dependentSequences);

            Assert.IsFalse(_claimStrategy.HasAvailableCapacity(1, dependentSequences));
        }

        [Test]
        public void ShouldNotReturnNextClaimSequenceUntilBufferHasReserve()
        {
            var dependentSequence = new Sequence(Sequence.InitialCursorValue);
            var dependentSequences = new[] { dependentSequence };
            _claimStrategy.SetSequence(BufferSize - 1L, dependentSequences);

            var done = new Volatile.Boolean(false);
            var beforeLatch = new ManualResetEvent(false);
            var afterLatch = new ManualResetEvent(false);

            new Thread(() =>
                           {
                               beforeLatch.Set();

                               Assert.AreEqual(_claimStrategy.BufferSize,
                                               _claimStrategy.IncrementAndGet(dependentSequences));

                               done.WriteFullFence(true);
                               afterLatch.Set();
                           }).Start();

            beforeLatch.WaitOne();

            Thread.Sleep(1000);
            Assert.IsFalse(done.ReadFullFence());

            dependentSequence.Value = dependentSequence.Value + 1L;

            afterLatch.WaitOne();
            Assert.AreEqual(_claimStrategy.BufferSize, _claimStrategy.Sequence);
        }

        [Test]
        public void ShouldSerializePublishingOnTheCursor()
        {
            var dependentSequence = new Sequence(Sequence.InitialCursorValue);
            var dependentSequences = new[] {dependentSequence};

            var sequence = _claimStrategy.IncrementAndGet(dependentSequences);

            var cursor = new Sequence(Sequence.InitialCursorValue);
            _claimStrategy.SerialisePublishing(sequence, cursor, 1);

            Assert.AreEqual(sequence, cursor.Value);
        }

        [Test]
        public void ShouldSerialisePublishingOnTheCursorWhenTwoThreadsArePublishing()
        {
            var dependentSequence = new Sequence(Sequence.InitialCursorValue);
            var dependentSequences = new[] { dependentSequence };

            var threadSequences = new ConcurrentDictionary<string, long>();
            var cursor = new SequenceStub(Sequence.InitialCursorValue, threadSequences);

            var mre = new ManualResetEvent(false);

            var t1 = new Thread(
                () =>
                    {
                        var sequence = _claimStrategy.IncrementAndGet(dependentSequences);
                        mre.Set();

                        Thread.Sleep(1000);

                        _claimStrategy.SerialisePublishing(sequence, cursor, 1);
                    });

            var t2 = new Thread(
                () =>
                    {
                        mre.WaitOne();
                        var sequence = _claimStrategy.IncrementAndGet(dependentSequences);

                        _claimStrategy.SerialisePublishing(sequence, cursor, 1);
                    });

            t1.Name = "tOne";
            t2.Name = "tTwo";
            t1.Start();
            t2.Start();
            t1.Join();
            t2.Join();

            Assert.AreEqual(0, threadSequences["tOne"]);
            Assert.AreEqual(1, threadSequences["tTwo"]);
        }

        public class SequenceStub : Sequence
        {
            private readonly ConcurrentDictionary<string, long> _threadSequences = new ConcurrentDictionary<string, long>();

            public SequenceStub(long initialValue, ConcurrentDictionary<string, long> threadSequences)
                : base(initialValue)
            {
                _threadSequences = threadSequences;
            }

            public override void LazySet(long value)
            {
                var threadName = Thread.CurrentThread.Name;
                if ("tOne" == threadName || "tTwo" == threadName)
                {
                    _threadSequences[threadName] = value;
                }
                base.LazySet(value);
            }
        }
    }
}