using System.Threading;

namespace Disruptor;

/// <summary>
/// Spin strategy that uses a <see cref="SpinWait"/> for event processors waiting on a barrier.
/// </summary>
/// <remarks>
/// This strategy is a good compromise between performance and CPU resources.
/// Latency spikes can occur after quiet periods.
/// </remarks>
public sealed class SpinWaitWaitStrategy : IWaitStrategy
{
    public bool IsBlockingStrategy => false;

    public SequenceWaitResult WaitFor(long sequence, Sequence cursor, ISequence dependentSequence, CancellationToken cancellationToken)
    {
        return dependentSequence.SpinWaitFor(sequence, cancellationToken);
    }

    public void SignalAllWhenBlocking()
    {
    }
}
