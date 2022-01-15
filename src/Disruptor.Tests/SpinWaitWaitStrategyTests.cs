namespace Disruptor.Tests;

public class SpinWaitWaitStrategyTests : WaitStrategyFixture<SpinWaitWaitStrategy>
{
    protected override SpinWaitWaitStrategy CreateWaitStrategy()
    {
        return new SpinWaitWaitStrategy();
    }
}