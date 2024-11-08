namespace Disruptor.Tests;

public class AsyncWaitStrategyTestsWithoutTimeout : AsyncWaitStrategyTests
{
    protected override IAsyncSequenceWaitStrategy CreateWaitStrategy()
    {
        return new AsyncWaitStrategy();
    }
}
