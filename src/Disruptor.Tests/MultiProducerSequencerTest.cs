using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture]
public class MultiProducerSequencerTest
{
    private readonly MultiProducerSequencer _publisher;

    public MultiProducerSequencerTest()
    {
        _publisher = new MultiProducerSequencer(1024);
    }

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