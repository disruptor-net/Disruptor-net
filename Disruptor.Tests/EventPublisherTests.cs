using System;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class EventPublisherTests
    {
        private const int BufferSize = 32;
        private const long ValueAdd = 29L;

        private readonly Func<LongEvent, long, LongEvent> _translator = (evt, seq) =>
        {
            evt.Value = seq + ValueAdd;
            return evt;
        };


        [Test]
        public void ShouldPublishEvent()
        {

            var ringBuffer = new RingBuffer<LongEvent>(()=>new LongEvent(0), BufferSize);
            ringBuffer.SetGatingSequences(new NoOpEventProcessor(ringBuffer).Sequence);
            var eventPublisher = new EventPublisher<LongEvent>(ringBuffer);


            eventPublisher.PublishEvent(_translator);
            eventPublisher.PublishEvent(_translator);

            Assert.AreEqual(0L + ValueAdd, ringBuffer[0].Value);
            Assert.AreEqual(1L + ValueAdd, ringBuffer[1].Value);
        }


        [Test]
        public void ShouldTryPublishEvent()
        {
            RingBuffer<LongEvent> ringBuffer = new RingBuffer<LongEvent>(()=>new LongEvent(0), BufferSize);
            ringBuffer.SetGatingSequences(new Sequence());
            EventPublisher<LongEvent> eventPublisher = new EventPublisher<LongEvent>(ringBuffer);



            for (int i = 0; i < BufferSize; i++)
            {
                Assert.IsTrue(eventPublisher.TryPublishEvent(_translator, 1));
            }

            for (int i = 0; i < BufferSize; i++)
            {
                Assert.AreEqual(i + ValueAdd, ringBuffer[i].Value);
            }

            Assert.IsFalse(eventPublisher.TryPublishEvent(_translator, 1));
    }

    }
}