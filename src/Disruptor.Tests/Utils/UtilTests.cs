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
        public void ShouldReadObjectAtIndex()
        {
            var array = Enumerable.Range(0, 2000).Select(x => new StubEvent(x)).ToArray();

            for (var i = 0; i < array.Length; i++)
            {
                var evt = Util.Read<StubEvent>(array, i);

                Assert.AreEqual(new StubEvent(i), evt);
            }
        }

        [Test]
        public void ShouldReadValueAtIndex()
        {
            var array = Enumerable.Range(0, 2000).Select(x => new StubValueEvent(x)).ToArray();

            for (var i = 0; i < array.Length; i++)
            {
                var evt = Util.ReadValue<StubValueEvent>(array, i);

                Assert.AreEqual(new StubValueEvent(i), evt);
            }
        }

        [Test]
        public void ShouldMutateValueAtIndex()
        {
            var array = Enumerable.Range(0, 2000).Select(x => new StubValueEvent(x)).ToArray();

            for (var i = 0; i < array.Length; i++)
            {
                ref var evt = ref Util.ReadValue<StubValueEvent>(array, i);
                evt.TestString = evt.Value.ToString();
            }

            for (var i = 0; i < array.Length; i++)
            {
                var evt = Util.ReadValue<StubValueEvent>(array, i);

                Assert.AreEqual(evt.Value.ToString(), evt.TestString);
            }
        }
    }
}
