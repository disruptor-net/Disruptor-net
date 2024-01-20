using System.Threading;

namespace Disruptor;

/// <summary>
/// Non-blocking wait strategy that uses a <see cref="SpinWait"/>.
/// </summary>
/// <remarks>
/// This strategy is a good compromise between performance and CPU resources.
/// Latency spikes can occur after quiet periods.
/// </remarks>
public sealed class SpinWaitWaitStrategy : IWaitStrategy
{
    public bool IsBlockingStrategy => false;

    public SequenceWaitResult WaitFor(long sequence, DependentSequenceGroup dependentSequences, CancellationToken cancellationToken)
    {
        return dependentSequences.SpinWaitFor(sequence, cancellationToken);
    }

    public void SignalAllWhenBlocking()
    {
    }
}
