using System;
using System.Linq;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests.Utils
{
    [TestFixture]
    public class UtilTests
    {
        [Test]
        public void ShouldReturnNextPowerOfTwo()
        {
            var powerOfTwo = 1000.CeilingNextPowerOfTwo();

            Assert.AreEqual(1024, powerOfTwo);
        }

        [Test]
        public void ShouldReturnExactPowerOfTwo()
        {
            var powerOfTwo = 1024.CeilingNextPowerOfTwo();

            Assert.AreEqual(1024, powerOfTwo);
        }

        [Test]
        public void ShouldReturnMinimumSequence()
        {
            var sequences = new[] {new Sequence(11), new Sequence(4), new Sequence(13)};

            Assert.AreEqual(4L, Util.GetMinimumSequence(sequences));
        }

        [Test]
        public void ShouldReturnLongMaxWhenNoEventProcessors()
        {
            var sequences = new Sequence[0];

            Assert.AreEqual(long.MaxValue, Util.GetMinimumSequence(sequences));
        }

        [Test]
        public void ShouldReadArrayElement()
        {
            var array = Enumerable.Range(0, 100)
                                  .Select(x => new LongEvent())
                                  .ToArray();

            for (var index = 0; index < array.Length; index++)
            {
                Assert.AreEqual(array[index], Util.Read<LongEvent>(array, index));
            }
        }
    }
}
