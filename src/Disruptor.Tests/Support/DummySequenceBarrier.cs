namespace Disruptor.Tests.Support;

public class DummySequenceBarrier : ICancellableBarrier
{
    public void Dispose()
    {
    }

    public void CancelProcessing()
    {
    }
}
