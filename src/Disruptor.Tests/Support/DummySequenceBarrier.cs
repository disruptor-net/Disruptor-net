using System.Threading;

namespace Disruptor.Tests.Support;

public class DummySequenceBarrier : ISequenceBarrier
{
    public SequenceWaitResult WaitFor(long sequence)
    {
        return 0;
    }

    public DependentSequenceGroup DependentSequences { get; } = new(new Sequence());

    public CancellationToken CancellationToken => default;

    public void ResetProcessing()
    {
    }

    public void CancelProcessing()
    {
    }
}
