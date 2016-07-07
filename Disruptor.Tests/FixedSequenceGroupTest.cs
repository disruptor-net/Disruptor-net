using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class FixedSequenceGroupTest
    {
        [Test]
        public void ShouldReturnMinimumOf2Sequences()
        {
            var sequence1 = new Sequence(34);
            var sequnece2 = new Sequence(47);
            var group = new FixedSequenceGroup(new[] { sequence1, sequnece2 });

            Assert.That(group.Value, Is.EqualTo(34L));
            sequence1.Value = 35;
            Assert.That(group.Value, Is.EqualTo(35L));
            sequence1.Value = 48;
            Assert.That(group.Value, Is.EqualTo(47L));
        }
    }
}
