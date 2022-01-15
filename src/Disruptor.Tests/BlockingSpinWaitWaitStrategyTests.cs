namespace Disruptor.Tests;

public class BlockingSpinWaitWaitStrategyTests : WaitStrategyFixture<BlockingSpinWaitWaitStrategy>
{
    protected override BlockingSpinWaitWaitStrategy CreateWaitStrategy()
    {
        return new BlockingSpinWaitWaitStrategy();
    }
}