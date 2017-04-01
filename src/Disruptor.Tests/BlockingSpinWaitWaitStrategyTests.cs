using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class BlockingSpinWaitWaitStrategyTests
    {
        [Test]
        public void ShouldWaitForValue()
        {
            WaitStrategyTestUtil.AssertWaitForWithDelayOf(50, new BlockingSpinWaitWaitStrategy());
        }
    }
}