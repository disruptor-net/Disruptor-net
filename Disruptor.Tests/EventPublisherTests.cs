using System;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class EventPublisherTests
    {
        [Test]
        public void ShouldPublishEvent()
        {
            const long valueAdd = 29L;

            var ringBuffer = new RingBuffer<LongEvent>(()=>new LongEvent(0), 32);
            ringBuffer.SetGatingSequences(new NoOpEventProcessor(ringBuffer).Sequence);
            var eventPublisher = new EventPublisher<LongEvent>(ringBuffer);

            Func<LongEvent, long, LongEvent> translator = (evt, seq) =>
                                                              {
                                                                  evt.Value = seq + valueAdd;
                                                                  return evt;
                                                              };

            eventPublisher.PublishEvent(translator);
            eventPublisher.PublishEvent(translator);

            Assert.AreEqual(0L + valueAdd, ringBuffer[0].Value);
            Assert.AreEqual(1L + valueAdd, ringBuffer[1].Value);
        }
    }
}