namespace Disruptor.Tests;

public class AsyncWaitStrategyTestsWithoutTimeout : AsyncWaitStrategyTests
{
    protected override IAsyncWaitStrategy CreateWaitStrategy()
    {
        return new AsyncWaitStrategy();
    }
}
