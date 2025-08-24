namespace Disruptor.Tests;

public class YieldingWaitStrategyTests : WaitStrategyFixture<YieldingWaitStrategy>
{
    protected override YieldingWaitStrategy CreateWaitStrategy()
    {
        return new YieldingWaitStrategy();
    }
}

public class YieldingWaitStrategyTests_Ipc : IpcWaitStrategyFixture<YieldingWaitStrategy>
{
    protected override YieldingWaitStrategy CreateWaitStrategy()
    {
        return new YieldingWaitStrategy();
    }
}
