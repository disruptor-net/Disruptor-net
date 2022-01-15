namespace Disruptor.Tests;

public class YieldingWaitStrategyTests : WaitStrategyFixture<YieldingWaitStrategy>
{
    protected override YieldingWaitStrategy CreateWaitStrategy()
    {
        return new YieldingWaitStrategy();
    }
}