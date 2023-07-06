namespace Disruptor.Tests;

public class HybridSpinWaitStrategyTestsSpinWait : WaitStrategyFixture<HybridSpinWaitStrategy>
{
    protected override HybridSpinWaitStrategy CreateWaitStrategy()
    {
        return new HybridSpinWaitStrategy();
    }
}
