using System;

namespace Disruptor.Tests;

public class ObsoleteBlockingWaitStrategyTests : WaitStrategyFixture<ObsoleteBlockingWaitStrategy>
{
    protected override ObsoleteBlockingWaitStrategy CreateWaitStrategy()
    {
        return new ObsoleteBlockingWaitStrategy();
    }
}
