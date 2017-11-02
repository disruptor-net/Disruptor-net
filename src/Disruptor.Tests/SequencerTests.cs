using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture(ProducerType.Single)]
    [TestFixture(ProducerType.Multi)]
    public class SequencerTests
    {
        private const int _bufferSize = 16;
        private readonly ProducerType _producerType;
        private Sequencer _sequencer;
        private Sequence _gatingSequence;

        public SequencerTests(ProducerType producerType)
        {
            _producerType = producerType;
        }

        private Sequencer NewProducer(ProducerType producerType, int bufferSize, IWaitStrategy waitStrategy)
        {
            switch (producerType)
            {
                case ProducerType.Single:
                    return new SingleProducerSequencer(bufferSize, waitStrategy);
                case ProducerType.Multi:
                    return new MultiProducerSequencer(bufferSize, waitStrategy);
                default:
                    throw new ArgumentOutOfRangeException(nameof(producerType), producerType, null);
            }
        }

        [SetUp]
        public void SetUp()
        {
            _gatingSequence = new Sequence();
            _sequencer = NewProducer(_producerType, _bufferSize, new BlockingWaitStrategy());
        }

        [Test]
        public void ShouldStartWithInitialValue()
        {
            Assert.AreEqual(0, _sequencer.Next());
        }

        [Test]
        public void ShouldBatchClaim()
        {
            Assert.AreEqual(3, _sequencer.Next(4));
        }

        [Test]
        public void ShouldIndicateHasAvailableCapacity()
        {
            _sequencer.AddGatingSequences(_gatingSequence);

            Assert.IsTrue(_sequencer.HasAvailableCapacity(1));
            Assert.IsTrue(_sequencer.HasAvailableCapacity(_bufferSize));
            Assert.False(_sequencer.HasAvailableCapacity(_bufferSize + 1));

            _sequencer.Publish(_sequencer.Next());

            Assert.IsTrue(_sequencer.HasAvailableCapacity(_bufferSize - 1));
            Assert.False(_sequencer.HasAvailableCapacity(_bufferSize));
        }

        [Test]
        public void ShouldIndicateNoAvailableCapacity()
        {
            _sequencer.AddGatingSequences(_gatingSequence);

            var sequence = _sequencer.Next(_bufferSize);
            _sequencer.Publish(sequence - (_bufferSize - 1), sequence);

            Assert.IsFalse(_sequencer.HasAvailableCapacity(1));
        }

        [Test]
        public void ShouldHoldUpPublisherWhenBufferIsFull()
        {
            _sequencer.AddGatingSequences(_gatingSequence);
            var sequence = _sequencer.Next(_bufferSize);
            _sequencer.Publish(sequence - (_bufferSize - 1), sequence);

            var waitingSignal = new ManualResetEvent(false);
            var doneSignal = new ManualResetEvent(false);

            var expectedFullSequence = Sequence.InitialCursorValue + _sequencer.BufferSize;
            Assert.That(_sequencer.Cursor, Is.EqualTo(expectedFullSequence));

            RunAsync(() =>
                     {
                         waitingSignal.Set();

                         var next = _sequencer.Next();
                         _sequencer.Publish(next);

                         doneSignal.Set();
                     });

            waitingSignal.WaitOne(TimeSpan.FromMilliseconds(500));
            Assert.That(_sequencer.Cursor, Is.EqualTo(expectedFullSequence));

            _gatingSequence.SetValue(Sequence.InitialCursorValue + 1L);

            doneSignal.WaitOne(TimeSpan.FromMilliseconds(500));
            Assert.That(_sequencer.Cursor, Is.EqualTo(expectedFullSequence + 1L));
        }

        [Test]
        [ExpectedException(typeof(InsufficientCapacityException))]
        public void ShouldThrowInsufficientCapacityExceptionWhenSequencerIsFull()
        {
            _sequencer.AddGatingSequences(_gatingSequence);

            for (var i = 0; i < _bufferSize; i++)
            {
                _sequencer.Next();
            }
            _sequencer.TryNext();
        }

        [Test]
        public void ShouldCalculateRemainingCapacity()
        {
            _sequencer.AddGatingSequences(_gatingSequence);

            Assert.That(_sequencer.GetRemainingCapacity(), Is.EqualTo(_bufferSize));

            for (var i = 1; i < _bufferSize; i++)
            {
                _sequencer.Next();
                Assert.That(_sequencer.GetRemainingCapacity(), Is.EqualTo(_bufferSize - i));
            }
        }

        [Test]
        public void ShoundNotBeAvailableUntilPublished()
        {
            var next = _sequencer.Next(6);

            for (var i = 0; i <= 5; i++)
            {
                Assert.That(_sequencer.IsAvailable(i), Is.False);
            }

            _sequencer.Publish(next - (6 - 1), next);

            for (var i = 0; i <= 5; i++)
            {
                Assert.That(_sequencer.IsAvailable(i), Is.True);
            }

            Assert.That(_sequencer.IsAvailable(6), Is.False);
        }

        [Test]
        public void ShouldNotifyWaitStrategyOnPublish()
        {
            var waitStrategy = new DummyWaitStrategy();
            var sequencer = NewProducer(_producerType, _bufferSize, waitStrategy);

            sequencer.Publish(sequencer.Next());

            Assert.That(waitStrategy.SignalAllWhenBlockingCalls, Is.EqualTo(1));
        }

        [Test]
        public void ShouldNotifyWaitStrategyOnPublishBatch()
        {
            var waitStrategy = new DummyWaitStrategy();
            var sequencer = NewProducer(_producerType, _bufferSize, waitStrategy);

            var next = _sequencer.Next(4);
            sequencer.Publish(next - (4 - 1), next);

            Assert.That(waitStrategy.SignalAllWhenBlockingCalls, Is.EqualTo(1));
        }

        [Test]
        public void ShouldWaitOnPublication()
        {
            var barrier = _sequencer.NewBarrier();

            var next = _sequencer.Next(10);
            var lo = next - (10 - 1);
            var mid = next - 5;

            for (var l = lo; l < mid; l++)
            {
                _sequencer.Publish(l);
            }

            Assert.That(barrier.WaitFor(-1), Is.EqualTo(mid - 1));

            for (var l = mid; l <= next; l++)
            {
                _sequencer.Publish(l);
            }
            Assert.That(barrier.WaitFor(-1), Is.EqualTo(next));
        }

        [Test]
        public void ShouldTryNext()
        {
            _sequencer.AddGatingSequences(_gatingSequence);

            for (int i = 0; i < _bufferSize; i++)
            {
                _sequencer.Publish(_sequencer.TryNext());
            }

            try
            {
                _sequencer.TryNext();
                throw new ApplicationException("Should of thrown: " + nameof(InsufficientCapacityException));
            }
            catch (InsufficientCapacityException e)
            {
                // No-op
            }
        }

        [Test]
        public void ShouldClaimSpecificSequence()
        {
            long sequence = 14L;

            _sequencer.Claim(sequence);
            _sequencer.Publish(sequence);
            Assert.That(_sequencer.Next(), Is.EqualTo(sequence + 1));
        }
        
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void ShouldNotAllowBulkNextLessThanZero()
        {
            _sequencer.Next(-1);
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void ShouldNotAllowBulkNextOfZero()
        {
            _sequencer.Next(0);
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]

        public void ShouldNotAllowBulkTryNextLessThanZero()
        {
            _sequencer.TryNext(-1);
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]

        public void ShouldNotAllowBulkTryNextOfZero()
        {
            _sequencer.TryNext(0);
        }

        private Task RunAsync(Action action)
        {
            return Task.Factory.StartNew(action);
        }
    }
}