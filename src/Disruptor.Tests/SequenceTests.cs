using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture]
public class SequenceTests
{
    [Test]
    public void ShouldReturnChangedValueAfterAddAndGet()
    {
        var sequence = new Sequence(0);

        Assert.That(sequence.AddAndGet(10), Is.EqualTo((long)10));
        Assert.That(sequence.Value, Is.EqualTo((long)10));
    }

    [Test]
    public void ShouldReturnIncrementedValueAfterIncrementAndGet()
    {
        var sequence = new Sequence(0);

        Assert.That(sequence.IncrementAndGet(), Is.EqualTo((long)1));
        Assert.That(sequence.Value, Is.EqualTo((long)1));
    }
}
