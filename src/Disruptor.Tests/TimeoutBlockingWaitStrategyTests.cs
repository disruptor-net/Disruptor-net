using System;

namespace Disruptor.Tests;

public class TimeoutBlockingWaitStrategyTests : TimeoutWaitStrategyFixture<TimeoutBlockingWaitStrategy>
{
    protected override TimeoutBlockingWaitStrategy CreateWaitStrategy(TimeSpan timeout)
    {
        return new TimeoutBlockingWaitStrategy(timeout);
    }
}