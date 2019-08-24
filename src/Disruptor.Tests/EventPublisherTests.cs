using System;
using Disruptor.Tests.Support;
using NUnit.Framework;

#pragma warning disable 618

namespace Disruptor.Tests
{
    [TestFixture]
    public class EventPublisherTests : IEventTranslator<LongEvent>
    {
        private const int _bufferSize = 32;
        private const long _valueAdd = 29L;
        private RingBuffer<LongEvent> _ringBuffer;

        [SetUp]
        public void SetUp()
        {
            _ringBuffer = RingBuffer<LongEvent>.CreateMultiProducer(() => new LongEvent(), _bufferSize);
        }

        [Test]
        public void ShouldPublishEvent()
        {
            _ringBuffer.AddGatingSequences(new NoOpEventProcessor<LongEvent>(_ringBuffer).Sequence);

            _ringBuffer.PublishEvent(this);
            _ringBuffer.PublishEvent(this);

            Assert.AreEqual(0L + _valueAdd, _ringBuffer[0].Value);
            Assert.AreEqual(1L + _valueAdd, _ringBuffer[1].Value);
        }

        [Test]
        public void ShouldTryPublishEvent()
        {
            _ringBuffer.AddGatingSequences(new Sequence());

            for (var i = 0; i < _bufferSize; i++)
            {
                Assert.IsTrue(_ringBuffer.TryPublishEvent(this));
            }

            for (var i = 0; i < _bufferSize; i++)
            {
                Assert.That(_ringBuffer[i].Value, Is.EqualTo(i + _valueAdd));
            }

            Assert.IsFalse(_ringBuffer.TryPublishEvent(this));
        }

        public void TranslateTo(LongEvent eventData, long sequence)
        {
            eventData.Value = sequence + 29;
        }
    }
}
