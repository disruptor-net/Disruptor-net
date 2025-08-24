using System;

namespace Disruptor;

public interface ICancellableBarrier
{
    void CancelProcessing();
}
