using System;
using NUnit.Framework;

namespace Disruptor.Tests.Support
{
    public static class WaitStrategyTestUtil
    {
        public static void AssertWaitForWithDelayOf(long sleepTimeMillis, IWaitStrategy waitStrategy)
        {
            var sequenceUpdater = new SequenceUpdater(sleepTimeMillis, waitStrategy);
            var task = sequenceUpdater.Start();

            sequenceUpdater.WaitForStartup();
            
            var cursor = new Sequence(0);
            var sequence = waitStrategy.WaitFor(0, cursor, sequenceUpdater.Sequence, default);

            Assert.That(sequence, Is.EqualTo(new SequenceWaitResult(0L)));
            Assert.True(task.Wait(TimeSpan.FromSeconds(1)));
        }
    }
}
