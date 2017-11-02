using NUnit.Framework;
using static Disruptor.Tests.Support.WaitStrategyTestUtil;

namespace Disruptor.Tests
{
    [TestFixture]
    public class BusySpinWaitStrategyTests
    {
        [Test]
        public void ShouldWaitForValue()
        {
            AssertWaitForWithDelayOf(50, new BusySpinWaitStrategy());
        }
    }
}