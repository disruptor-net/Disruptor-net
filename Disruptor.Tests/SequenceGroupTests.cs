using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class SequenceGroupTests
    {
        [Test]
        public void ShouldReturnMaxSequenceWhenEmptyGroup()
        {
            var sequenceGroup = new SequenceGroup();
            Assert.AreEqual(long.MaxValue, sequenceGroup.Value);
        }

        [Test]
        public void ShouldAddOneSequenceToGroup()
        {
            var sequence = new Sequence(7L);
            var sequenceGroup = new SequenceGroup();

            sequenceGroup.Add(sequence);

            Assert.AreEqual(sequence.Value, sequenceGroup.Value);
        }

        [Test]
        public void ShouldReportTheMinimumSequenceForGroupOfTwo()
        {
            var sequenceThree = new Sequence(3L);
            var sequenceSeven = new Sequence(7L);
            var sequenceGroup = new SequenceGroup();

            sequenceGroup.Add(sequenceSeven);
            sequenceGroup.Add(sequenceThree);

            Assert.AreEqual(sequenceThree.Value, sequenceGroup.Value);
        }

        [Test]
        public void ShouldReportSizeOfGroup()
        {
            var sequenceGroup = new SequenceGroup();
            sequenceGroup.Add(new Sequence());
            sequenceGroup.Add(new Sequence());
            sequenceGroup.Add(new Sequence());

            Assert.AreEqual(3, sequenceGroup.Size);
        }

        [Test]
        public void ShouldRemoveSequenceFromGroup()
        {
            var sequenceThree = new Sequence(3L);
            var sequenceSeven = new Sequence(7L);
            var sequenceGroup = new SequenceGroup();

            sequenceGroup.Add(sequenceSeven);
            sequenceGroup.Add(sequenceThree);

            Assert.AreEqual(sequenceThree.Value, sequenceGroup.Value);

            Assert.IsTrue(sequenceGroup.Remove(sequenceThree));
            Assert.AreEqual(sequenceSeven.Value, sequenceGroup.Value);
            Assert.AreEqual(1, sequenceGroup.Size);
        }

        [Test]
        public void ShouldSetGroupSequenceToSameValue()
        {
            var sequenceThree = new Sequence(3L);
            var sequenceSeven = new Sequence(7L);
            var sequenceGroup = new SequenceGroup();

            sequenceGroup.Add(sequenceSeven);
            sequenceGroup.Add(sequenceThree);

            const long expectedSequence = 11L;
            sequenceGroup.Value = expectedSequence;

            Assert.AreEqual(expectedSequence, sequenceThree.Value);
            Assert.AreEqual(expectedSequence, sequenceSeven.Value);
        }
    }
}