using Disruptor.Tests.Support;
using Moq;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class RingBufferWithMocksTest
    {
        private RingBuffer<StubEvent> _ringBuffer;
        private ISequencer _sequencer;
        private Mock<ISequencer> _sequencerMock;

        [SetUp]
        public void SetUp()
        {
            _sequencerMock = new Mock<ISequencer>();
            _sequencer = _sequencerMock.Object;

            _sequencerMock.SetupGet(x => x.BufferSize).Returns(16);
            _ringBuffer = new RingBuffer<StubEvent>(StubEvent.EventFactory, _sequencer);
        }

        [Test]
        public void ShouldDelgateNextAndPublish()
        {
            _sequencerMock.Setup(x => x.Next()).Returns(34L);

            _ringBuffer.Publish(_ringBuffer.Next());

            _sequencerMock.Verify(x => x.Next(), Times.Once());
            _sequencerMock.Verify(x => x.Publish(34L), Times.Once());
        }

        [Test]
        public void ShouldDelgateTryNextAndPublish()
        {
            _sequencerMock.Setup(x => x.TryNext()).Returns(34L);

            _ringBuffer.Publish(_ringBuffer.TryNext());

            _sequencerMock.Verify(x => x.TryNext(), Times.Once());
            _sequencerMock.Verify(x => x.Publish(34L), Times.Once());
        }

        [Test]
        public void ShouldDelgateNextNAndPublish()
        {
            _sequencerMock.Setup(x => x.Next(10)).Returns(34L);

            long hi = _ringBuffer.Next(10);
            _ringBuffer.Publish(hi - 9, hi);

            _sequencerMock.Verify(x => x.Next(10), Times.Once());
            _sequencerMock.Verify(x => x.Publish(25L, 34L), Times.Once());
        }

        [Test]
        public void ShouldDelgateTryNextNAndPublish()
        {
            _sequencerMock.Setup(x => x.TryNext(10)).Returns(34L);

            long hi = _ringBuffer.TryNext(10);
            _ringBuffer.Publish(hi - 9, hi);

            _sequencerMock.Verify(x => x.TryNext(10), Times.Once());
            _sequencerMock.Verify(x => x.Publish(25L, 34L), Times.Once());
        }
    }
}
