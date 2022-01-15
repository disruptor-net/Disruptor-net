using System.Threading;

namespace Disruptor.Tests.Support;

public class DummySequenceBarrier : ISequenceBarrier
{
    public SequenceWaitResult WaitFor(long sequence)
    {
        return 0;
    }

    public long Cursor => 0;

    public CancellationToken CancellationToken => default;

    public void ResetProcessing()
    {
    }

    public void CancelProcessing()
    {
    }
}