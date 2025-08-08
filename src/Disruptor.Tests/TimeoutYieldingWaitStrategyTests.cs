using System;

namespace Disruptor.Tests;

public class TimeoutYieldingWaitStrategyTests : TimeoutWaitStrategyFixture<TimeoutYieldingWaitStrategy>
{
    protected override TimeoutYieldingWaitStrategy CreateWaitStrategy(TimeSpan timeout)
    {
        return new TimeoutYieldingWaitStrategy(timeout);
    }
}
