using System.Threading;

namespace Disruptor.Tests.Support;

public class DummySequenceWaiter(DependentSequenceGroup dependentSequences) : ISequenceWaiter
{
    public DependentSequenceGroup DependentSequences => dependentSequences;

    public SequenceWaitResult WaitForResult { get; set; }

    public SequenceWaitResult WaitFor(long sequence, CancellationToken cancellationToken)
    {
        return WaitForResult;
    }

    public void Cancel()
    {
    }

    public void Dispose()
    {
    }
}
