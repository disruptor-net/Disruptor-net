using System;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class PhasedBackoffWaitStrategyTest
    {
        [Test]
        public void ShouldHandleImmediateSequenceChange()
        {
            WaitStrategyTestUtil.AssertWaitForWithDelayOf(0, PhasedBackoffWaitStrategy.WithLock(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)));
            WaitStrategyTestUtil.AssertWaitForWithDelayOf(0, PhasedBackoffWaitStrategy.WithSleep(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)));
        }

        [Test]
        public void ShouldHandleSequenceChangeWithOneMillisecondDelay()
        {
            WaitStrategyTestUtil.AssertWaitForWithDelayOf(1, PhasedBackoffWaitStrategy.WithLock(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)));
            WaitStrategyTestUtil.AssertWaitForWithDelayOf(1, PhasedBackoffWaitStrategy.WithSleep(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)));
        }

        [Test]
        public void ShouldHandleSequenceChangeWithTwoMillisecondDelay()
        {
            WaitStrategyTestUtil.AssertWaitForWithDelayOf(2, PhasedBackoffWaitStrategy.WithLock(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)));
            WaitStrategyTestUtil.AssertWaitForWithDelayOf(2, PhasedBackoffWaitStrategy.WithSleep(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)));
        }

        [Test]
        public void ShouldHandleSequenceChangeWithTenMillisecondDelay()
        {
            WaitStrategyTestUtil.AssertWaitForWithDelayOf(10, PhasedBackoffWaitStrategy.WithLock(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)));
            WaitStrategyTestUtil.AssertWaitForWithDelayOf(10, PhasedBackoffWaitStrategy.WithSleep(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)));
        }
    }
}