using System.Threading.Tasks;
using NUnit.Framework;

namespace Disruptor.Tests
{
    public static class WaitStrategyTestUtil
    {
        public static void AssertWaitForWithDelayOf(long sleepTimeMillis, IWaitStrategy waitStrategy)
        {
            var sequenceUpdater = new SequenceUpdater(sleepTimeMillis, waitStrategy);
            Task.Factory.StartNew(sequenceUpdater.Run);
            sequenceUpdater.WaitForStartup();
            var cursor = new Sequence(0);
            var sequence = waitStrategy.WaitFor(0, cursor, sequenceUpdater.Sequence, new DummySequenceBarrier());

            Assert.That(sequence, Is.EqualTo(0L));
        }
    }
}