using System.Threading;

namespace Disruptor.Tests.Support;

public class DummyWaitStrategy : IWaitStrategy
{
    public DummyWaitStrategy(bool isBlockingStrategy = true)
    {
        IsBlockingStrategy = isBlockingStrategy;
    }

    public bool IsBlockingStrategy { get; private set; }

    public int SignalAllWhenBlockingCalls { get; private set; }

    public SequenceWaitResult WaitFor(long sequence, Sequence cursor, ISequence dependentSequence, CancellationToken cancellationToken)
    {
        return 0;
    }

    public void SignalAllWhenBlocking()
    {
        SignalAllWhenBlockingCalls++;
    }
}