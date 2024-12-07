using System;
using System.Threading;

namespace Disruptor;

/// <summary>
/// Non-blocking wait strategy that uses a busy spin loop.
/// </summary>
/// <remarks>
/// This strategy will use CPU resources to avoid system calls which can introduce latency jitter. It is best
/// used when threads can be bound to specific CPU cores.
/// </remarks>
#pragma warning disable CS0618 // Type or member is obsolete
public sealed class BusySpinWaitStrategy : ISequenceWaitStrategy, IWaitStrategy
#pragma warning restore CS0618 // Type or member is obsolete
{
    public bool IsBlockingStrategy => false;

    public ISequenceWaiter NewSequenceWaiter(IEventHandler? eventHandler, DependentSequenceGroup dependentSequences)
    {
        return new SequenceWaiter(dependentSequences);
    }

    SequenceWaitResult IWaitStrategy.WaitFor(long sequence, DependentSequenceGroup dependentSequences, CancellationToken cancellationToken)
        => throw new NotSupportedException("IWaitStrategy must be converted to " + nameof(ISequenceWaitStrategy) + " before use.");

    public void SignalAllWhenBlocking()
    {
    }

    private class SequenceWaiter(DependentSequenceGroup dependentSequences) : ISequenceWaiter
    {
        public DependentSequenceGroup DependentSequences => dependentSequences;

        public SequenceWaitResult WaitFor(long sequence, CancellationToken cancellationToken)
        {
            long availableSequence;

            while ((availableSequence = dependentSequences.Value) < sequence)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return availableSequence;
        }

        public void Cancel()
        {
        }

        public void Dispose()
        {
        }
    }
}
