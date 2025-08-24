using System;

namespace Disruptor;

public interface ICancellableBarrier : IDisposable
{
    void CancelProcessing();
}
