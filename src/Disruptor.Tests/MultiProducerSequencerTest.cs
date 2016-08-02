using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class MultiProducerSequencerTest
    {
        private readonly Sequencer _publisher = new MultiProducerSequencer(1024, new BlockingWaitStrategy());

        [Test]
        public void ShouldOnlyAllowMessagesToBeAvailableIfSpecificallyPublished() 
        {
            _publisher.Publish(3);
            _publisher.Publish(5);

            Assert.That(_publisher.IsAvailable(0), Is.EqualTo(false));
            Assert.That(_publisher.IsAvailable(1), Is.EqualTo(false));
            Assert.That(_publisher.IsAvailable(2), Is.EqualTo(false));
            Assert.That(_publisher.IsAvailable(3), Is.EqualTo(true));
            Assert.That(_publisher.IsAvailable(4), Is.EqualTo(false));
            Assert.That(_publisher.IsAvailable(5), Is.EqualTo(true));
            Assert.That(_publisher.IsAvailable(6), Is.EqualTo(false));
        }
    }
}
