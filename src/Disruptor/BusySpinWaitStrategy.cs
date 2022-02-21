using System.Threading;

namespace Disruptor;

/// <summary>
/// Busy Spin strategy that uses a busy spin loop for event processors waiting on a barrier.
/// </summary>
/// <remarks>
/// This strategy will use CPU resources to avoid syscalls which can introduce latency jitter. It is best
/// used when threads can be bound to specific CPU cores.
/// </remarks>
public sealed class BusySpinWaitStrategy : IWaitStrategy
{
    public bool IsBlockingStrategy => false;

    public SequenceWaitResult WaitFor(long sequence, Sequence cursor, ISequence dependentSequence, CancellationToken cancellationToken)
    {
        long availableSequence;

        while ((availableSequence = dependentSequence.Value) < sequence)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        return availableSequence;
    }

    public void SignalAllWhenBlocking()
    {
    }
}
