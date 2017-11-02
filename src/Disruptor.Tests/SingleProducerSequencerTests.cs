using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class SingleProducerSequencerTests
    {
        [Test]
        public void ShouldNotUpdateCursorDuringHasAvailableCapacity()
        {
            var sequencer = new SingleProducerSequencer(16, new BusySpinWaitStrategy());

            for (int i = 0; i < 32; i++)
            {
                var next = sequencer.Next();
                Assert.That(sequencer.Cursor, Is.Not.EqualTo(next));

                sequencer.HasAvailableCapacity(13);
                Assert.That(sequencer.Cursor, Is.Not.EqualTo(next));

                sequencer.Publish(next);
            }
        }
    }
}
