namespace Disruptor.Tests.Support;

public class DummyWaitStrategy : IWaitStrategy
{
    public bool IsBlockingStrategy { get; set; }

    public int SignalAllWhenBlockingCalls { get; private set; }

    public DummySequenceWaiter? LastSequenceWaiter { get; private set; }
    public IEventHandler? LastSequenceWaiterEventHandler { get; private set; }

    public ISequenceWaiter NewSequenceWaiter(IEventHandler? eventHandler, DependentSequenceGroup dependentSequences)
    {
        LastSequenceWaiterEventHandler = eventHandler;
        LastSequenceWaiter = new DummySequenceWaiter(dependentSequences);

        return LastSequenceWaiter;
    }

    public void SignalAllWhenBlocking()
    {
        SignalAllWhenBlockingCalls++;
    }
}
