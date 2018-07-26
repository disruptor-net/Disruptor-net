using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class ValueRingBufferWithAssertingStubTest
    {
        private ValueRingBuffer<StubValueEvent> _ringBuffer;
        private ISequencer _sequencer;

        [SetUp]
        public void SetUp()
        {
            _sequencer = new RingBufferWithAssertingStubTest.AssertingSequencer(16);

            _ringBuffer = new ValueRingBuffer<StubValueEvent>(StubValueEvent.EventFactory, _sequencer);
        }

        [Test]
        public void ShouldDelegateNextAndPublish()
        {
            _ringBuffer.Publish(_ringBuffer.Next());
        }

        [Test]
        public void ShouldDelegateTryNextAndPublish()
        {
            _ringBuffer.Publish(_ringBuffer.TryNext());
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
            long hi = _ringBuffer.TryNext(10);
            _ringBuffer.Publish(hi - 9, hi);
        }

        [Test]
        public void ShouldDelegateTryNextNOutAndPublish()
        {
            Assert.That(_ringBuffer.TryNext(10, out var hi), Is.True);
            _ringBuffer.Publish(hi - 9, hi);
        }
    }
}
