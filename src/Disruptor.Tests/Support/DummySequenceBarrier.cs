using System;

namespace Disruptor.Tests.Support;

public class DummySequenceBarrier : ICancellableBarrier, IDisposable
{
    public void Dispose()
    {
    }

    public void CancelProcessing()
    {
    }
}
