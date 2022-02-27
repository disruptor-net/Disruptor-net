namespace Disruptor.Tests;

public class BlockingWaitStrategyTests : WaitStrategyFixture<BlockingWaitStrategy>
{
    protected override BlockingWaitStrategy CreateWaitStrategy()
    {
        return new BlockingWaitStrategy();
    }
}
