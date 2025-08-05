namespace Disruptor.Tests.Support;

public class DummyWaitStrategy : IWaitStrategy
{
    public bool IsBlockingStrategy { get; set; }

    public int SignalAllWhenBlockingCalls { get; private set; }

    public DummySequenceWaiter? LastSequenceWaiter { get; private set; }
    public SequenceWaiterOwner? LastSequenceWaiterOwner { get; private set; }

    public ISequenceWaiter NewSequenceWaiter(SequenceWaiterOwner owner, DependentSequenceGroup dependentSequences)
    {
        LastSequenceWaiterOwner = owner;
        LastSequenceWaiter = new DummySequenceWaiter();

        return LastSequenceWaiter;
    }

    public void SignalAllWhenBlocking()
    {
        SignalAllWhenBlockingCalls++;
    }
}
