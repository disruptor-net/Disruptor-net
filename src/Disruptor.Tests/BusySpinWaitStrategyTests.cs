namespace Disruptor.Tests;

public class BusySpinWaitStrategyTests : WaitStrategyFixture<BusySpinWaitStrategy>
{
    protected override BusySpinWaitStrategy CreateWaitStrategy()
    {
        return new BusySpinWaitStrategy();
    }
}