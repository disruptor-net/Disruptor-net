using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class SequenceTests
    {
        [Test]
        public void ShouldReturnChangedValueAfterAddAndGet()
        {
            var sequence = new Sequence(0);

            Assert.AreEqual(10, sequence.AddAndGet(10));
            Assert.AreEqual(10, sequence.Value);
        }

        [Test]
        public void ShouldReturnIncrementedValueAfterIncrementAndGet()
        {
            var sequence = new Sequence(0);

            Assert.AreEqual(1, sequence.IncrementAndGet());
            Assert.AreEqual(1, sequence.Value);
        }
    }
}
