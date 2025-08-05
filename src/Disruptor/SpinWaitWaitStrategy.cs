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

    public ISequenceWaiter NewSequenceWaiter(SequenceWaiterOwner owner, DependentSequenceGroup dependentSequences)
    {
        return new SequenceWaiter(dependentSequences);
    }

    public void SignalAllWhenBlocking()
    {
    }

    private class SequenceWaiter(DependentSequenceGroup dependentSequences) : ISequenceWaiter
    {
        public SequenceWaitResult WaitFor(long sequence, CancellationToken cancellationToken)
        {
            return dependentSequences.SpinWaitFor(sequence, cancellationToken);
        }

        public void Cancel()
        {
        }

        public void Dispose()
        {
        }
    }
}
