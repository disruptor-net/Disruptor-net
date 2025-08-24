using System;

namespace Disruptor.Tests;

public class TimeoutYieldingWaitStrategyTests : TimeoutWaitStrategyFixture<TimeoutYieldingWaitStrategy>
{
    protected override TimeoutYieldingWaitStrategy CreateWaitStrategy(TimeSpan timeout)
    {
        return new TimeoutYieldingWaitStrategy(timeout);
    }
}

public class TimeoutYieldingWaitStrategyTests_Ipc : TimeoutIpcWaitStrategyFixture<TimeoutYieldingWaitStrategy>
{
    protected override TimeoutYieldingWaitStrategy CreateWaitStrategy(TimeSpan timeout)
    {
        return new TimeoutYieldingWaitStrategy(timeout);
    }
}
