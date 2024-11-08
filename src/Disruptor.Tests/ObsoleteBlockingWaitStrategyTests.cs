#pragma warning disable CS0618 // Type or member is obsolete

namespace Disruptor.Tests;

public class ObsoleteBlockingWaitStrategyTests : WaitStrategyFixture<ISequenceWaitStrategy>
{
    protected override ISequenceWaitStrategy CreateWaitStrategy()
    {
        return new ObsoleteBlockingWaitStrategy().ToSequenceWaitStrategy();
    }
}
