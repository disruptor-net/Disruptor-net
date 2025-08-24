namespace Disruptor.Tests;

public class SpinWaitWaitStrategyTests : WaitStrategyFixture<SpinWaitWaitStrategy>
{
    protected override SpinWaitWaitStrategy CreateWaitStrategy()
    {
        return new SpinWaitWaitStrategy();
    }
}

public class SpinWaitWaitStrategyTests_Ipc : IpcWaitStrategyFixture<SpinWaitWaitStrategy>
{
    protected override SpinWaitWaitStrategy CreateWaitStrategy()
    {
        return new SpinWaitWaitStrategy();
    }
}
