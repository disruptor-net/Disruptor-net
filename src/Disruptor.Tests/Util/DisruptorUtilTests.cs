using NUnit.Framework;

namespace Disruptor.Tests.Util
{
    [TestFixture]
    public class DisruptorUtilTests
    {
        [Test]
        public void ShouldReturnNextPowerOfTwo()
        {
            var powerOfTwo = DisruptorUtil.CeilingNextPowerOfTwo(1000);

            Assert.AreEqual(1024, powerOfTwo);
        }

        [Test]
        public void ShouldReturnExactPowerOfTwo()
        {
            var powerOfTwo = DisruptorUtil.CeilingNextPowerOfTwo(1024);

            Assert.AreEqual(1024, powerOfTwo);
        }

        [Test]
        public void ShouldReturnMinimumSequence()
        {
            var sequences = new[] {new Sequence(11), new Sequence(4), new Sequence(13)};

            Assert.AreEqual(4L, DisruptorUtil.GetMinimumSequence(sequences));
        }

        [Test]
        public void ShouldReturnLongMaxWhenNoEventProcessors()
        {
            var sequences = new Sequence[0];

            Assert.AreEqual(long.MaxValue, DisruptorUtil.GetMinimumSequence(sequences));
        }
    }
}
