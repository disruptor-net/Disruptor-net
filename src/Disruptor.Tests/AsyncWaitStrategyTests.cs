namespace Disruptor.Tests;

public class AsyncWaitStrategyTests : AsyncWaitStrategyFixture
{
    protected override IAsyncWaitStrategy CreateWaitStrategy()
    {
        return new AsyncWaitStrategy();
    }
}
