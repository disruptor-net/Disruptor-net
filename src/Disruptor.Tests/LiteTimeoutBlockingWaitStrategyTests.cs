using System;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class LiteTimeoutBlockingWaitStrategyTests : TimeoutWaitStrategyFixture<LiteTimeoutBlockingWaitStrategy>
    {
        protected override LiteTimeoutBlockingWaitStrategy CreateWaitStrategy(TimeSpan timeout)
        {
            return new LiteTimeoutBlockingWaitStrategy(timeout);
        }
    }
}
