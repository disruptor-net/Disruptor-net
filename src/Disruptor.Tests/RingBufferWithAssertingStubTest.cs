using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class RingBufferWithAssertingStubTest
    {
        private RingBuffer<StubEvent> _ringBuffer;
        private ISequencer _sequencer;

        [SetUp]
        public void SetUp()
        {
            _sequencer = new AssertingSequencer(16);

            _ringBuffer = new RingBuffer<StubEvent>(StubEvent.EventFactory, _sequencer);
        }

        [Test]
        public void ShouldDelegateNextAndPublish()
        {
            _ringBuffer.Publish(_ringBuffer.Next());
        }

        [Test]
        public void ShouldDelegateTryNextOutAndPublish()
        {
            Assert.That(_ringBuffer.TryNext(out var sequence), Is.True);
            _ringBuffer.Publish(sequence);
        }

        [Test]
        public void ShouldDelegateNextNAndPublish()
        {
            long hi = _ringBuffer.Next(10);
            _ringBuffer.Publish(hi - 9, hi);
        }

        [Test]
        public void ShouldDelegateTryNextNAndPublish()
        {
            _ringBuffer.TryNext(10, out var hi);
            _ringBuffer.Publish(hi - 9, hi);
        }

        [Test]
        public void ShouldDelegateTryNextNOutAndPublish()
        {
            Assert.That(_ringBuffer.TryNext(10, out var hi), Is.True);
            _ringBuffer.Publish(hi - 9, hi);
        }

        public class AssertingSequencer : ISequencer
        {
            private readonly int _size;
            private long _lastBatchSize = -1;
            private long _lastValue = -1;

            public AssertingSequencer(int size)
            {
                _size = size;
            }

            public int BufferSize => _size;

            public bool HasAvailableCapacity(int requiredCapacity) => requiredCapacity <= _size;

            public long GetRemainingCapacity() => _size;

            public long Next()
            {
                _lastValue = ThreadLocalRandom.Current.Next(0, 1000000);
                _lastBatchSize = 1;
                return _lastValue;
            }

            public long Next(int n)
            {
                _lastValue = ThreadLocalRandom.Current.Next(n, 1000000);
                _lastBatchSize = n;
                return _lastValue;
            }

            public bool TryNext(out long sequence)
            {
                sequence = Next();
                return true;
            }

            public bool TryNext(int n, out long sequence)
            {
                sequence = Next(n);
                return true;
            }

            public void Publish(long sequence)
            {
                Assert.That(sequence, Is.EqualTo(_lastValue));
                Assert.That(_lastBatchSize, Is.EqualTo(1L));
            }

            public void Publish(long lo, long hi)
            {
                Assert.That(hi, Is.EqualTo(_lastValue));
                Assert.That((hi - lo) + 1, Is.EqualTo(_lastBatchSize));
            }

            public long Cursor => _lastValue;

            public void Claim(long sequence)
            {
            }

            public bool IsAvailable(long sequence)
            {
                return false;
            }

            public void AddGatingSequences(params ISequence[] gatingSequences)
            {
            }

            public bool RemoveGatingSequence(ISequence sequence)
            {
                return false;
            }

            public ISequenceBarrier NewBarrier(params ISequence[] sequencesToTrack)
            {
                return null;
            }

            public long GetMinimumSequence()
            {
                return 0;
            }

            public long GetHighestPublishedSequence(long nextSequence, long availableSequence)
            {
                return 0;
            }

            public EventPoller<T> NewPoller<T>(IDataProvider<T> provider, params ISequence[] gatingSequences)
            {
                return null;
            }

            public ValueEventPoller<T> NewPoller<T>(IValueDataProvider<T> provider, params ISequence[] gatingSequences)
                where T : struct
            {
                return null;
            }
        }
    }
}
