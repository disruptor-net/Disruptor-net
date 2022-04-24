using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture]
public class MultiProducerSequencerTest : SequencerTests
{
    [Test]
    public void ShouldOnlyAllowMessagesToBeAvailableIfSpecificallyPublished()
    {
        var sequencer = new MultiProducerSequencer(32);

        sequencer.Publish(3);
        sequencer.Publish(5);

        Assert.That(sequencer.IsAvailable(0), Is.EqualTo(false));
        Assert.That(sequencer.IsAvailable(1), Is.EqualTo(false));
        Assert.That(sequencer.IsAvailable(2), Is.EqualTo(false));
        Assert.That(sequencer.IsAvailable(3), Is.EqualTo(true));
        Assert.That(sequencer.IsAvailable(4), Is.EqualTo(false));
        Assert.That(sequencer.IsAvailable(5), Is.EqualTo(true));
        Assert.That(sequencer.IsAvailable(6), Is.EqualTo(false));
    }

    [Test]
    public void ShouldGetHighestPublishedSequence([Range(2, 7)] int publishedSequence)
    {
        var sequencer = new MultiProducerSequencer(32);

        for (var i = 0; i <= 7; i++)
        {
            sequencer.Next();
        }

        for (var i = 0; i <= publishedSequence; i++)
        {
            sequencer.Publish(i);
        }

        Assert.That(sequencer.GetHighestPublishedSequence(3, 7), Is.EqualTo(publishedSequence));
    }

    protected override ISequencer NewSequencer(IWaitStrategy waitStrategy, int bufferSize = 16)
    {
        return new MultiProducerSequencer(bufferSize, waitStrategy);
    }
}
