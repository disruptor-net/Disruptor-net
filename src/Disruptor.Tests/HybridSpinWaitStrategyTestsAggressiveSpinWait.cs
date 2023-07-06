namespace Disruptor.Tests;

public class HybridSpinWaitStrategyTestsAggressiveSpinWait : WaitStrategyFixture<HybridSpinWaitStrategy>
{
    protected override HybridSpinWaitStrategy CreateWaitStrategy()
    {
        return new HybridSpinWaitStrategy();
    }

    protected override DependentSequenceGroup CreateDependentSequences(params Sequence[] dependentSequences)
    {
        var dependentSequenceGroup = base.CreateDependentSequences(dependentSequences);

        dependentSequenceGroup.Tag = HybridSpinWaitStrategy.AggressiveSpinWaitTag;

        return dependentSequenceGroup;
    }
}
