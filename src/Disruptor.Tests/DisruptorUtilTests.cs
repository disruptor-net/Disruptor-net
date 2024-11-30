using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture]
public class DisruptorUtilTests
{
    [Test]
    public void ShouldReturnNextPowerOfTwo()
    {
        var powerOfTwo = DisruptorUtil.CeilingNextPowerOfTwo(1000);

        Assert.That(powerOfTwo, Is.EqualTo(1024));
    }

    [Test]
    public void ShouldReturnExactPowerOfTwo()
    {
        var powerOfTwo = DisruptorUtil.CeilingNextPowerOfTwo(1024);

        Assert.That(powerOfTwo, Is.EqualTo(1024));
    }

    [Test]
    public void ShouldReturnMinimumSequence()
    {
        var sequences = new[] {new Sequence(11), new Sequence(4), new Sequence(13)};

        Assert.That(DisruptorUtil.GetMinimumSequence(sequences), Is.EqualTo(4L));
    }

    [Test]
    public void ShouldReturnLongMaxWhenNoEventProcessors()
    {
        var sequences = new Sequence[0];

        Assert.That(DisruptorUtil.GetMinimumSequence(sequences), Is.EqualTo(long.MaxValue));
    }
}
