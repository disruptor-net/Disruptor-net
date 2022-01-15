namespace Disruptor.Tests;

public class SleepingWaitStrategyTests : WaitStrategyFixture<SleepingWaitStrategy>
{
    protected override SleepingWaitStrategy CreateWaitStrategy()
    {
        return new SleepingWaitStrategy();
    }
}