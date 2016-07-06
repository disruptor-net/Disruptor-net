using NUnit.Framework;
using static Disruptor.Tests.WaitStrategyTestUtil;

namespace Disruptor.Tests
{
    [TestFixture]
    public class YieldingWaitStrategyTests
    {
        [Test]
        public void ShouldWaitForValue()
        {
            AssertWaitForWithDelayOf(50, new YieldingWaitStrategy());
        }
    }
}