using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class BatchPublisherTests
    {
        private readonly RingBuffer<StubEvent> _ringBuffer = new RingBuffer<StubEvent>(()=>new StubEvent(0), 32);
        private ISequenceBarrier _sequenceBarrier;

        [SetUp]
        public void SetUp()
        {
            _sequenceBarrier = _ringBuffer.NewBarrier();
            _ringBuffer.SetGatingSequences(new NoOpEventProcessor(_ringBuffer).Sequence);
        }

        [Test]
        public void ShouldClaimBatchAndPublishBack()
        {
            const int batchSize = 5;
            var batchDescriptor = _ringBuffer.NewBatchDescriptor(batchSize);

            _ringBuffer.Next(batchDescriptor);

            Assert.AreEqual(0L, batchDescriptor.Start);
            Assert.AreEqual(4L, batchDescriptor.End);
            Assert.AreEqual(Sequencer.InitialCursorValue, _ringBuffer.Cursor);

            _ringBuffer.Publish(batchDescriptor);

            Assert.AreEqual(batchSize - 1L, _ringBuffer.Cursor);
            Assert.AreEqual(batchSize - 1L, _sequenceBarrier.WaitFor(0L));
        }
    }
}