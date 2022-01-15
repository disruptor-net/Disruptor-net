using System;

namespace Disruptor.Tests;

public class PhasedBackoffWaitStrategyTestWithSleep : WaitStrategyFixture<PhasedBackoffWaitStrategy>
{
    protected override PhasedBackoffWaitStrategy CreateWaitStrategy()
    {
        return PhasedBackoffWaitStrategy.WithSleep(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1));
    }
}