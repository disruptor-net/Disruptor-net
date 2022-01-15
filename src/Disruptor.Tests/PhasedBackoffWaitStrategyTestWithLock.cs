using System;

namespace Disruptor.Tests
{
    public class PhasedBackoffWaitStrategyTestWithLock : WaitStrategyFixture<PhasedBackoffWaitStrategy>
    {
        protected override PhasedBackoffWaitStrategy CreateWaitStrategy()
        {
            return PhasedBackoffWaitStrategy.WithLock(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1));
        }
    }
}
