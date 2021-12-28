using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class SleepingWaitStrategyTests : WaitStrategyFixture<SleepingWaitStrategy>
    {
        public SleepingWaitStrategyTests()
            : base(new SleepingWaitStrategy())
        {
        }
    }
}
