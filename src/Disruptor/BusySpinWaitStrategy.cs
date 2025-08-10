using System.Threading;

namespace Disruptor;

/// <summary>
/// Non-blocking wait strategy that uses a busy spin loop.
/// </summary>
/// <remarks>
/// This strategy will use CPU resources to avoid system calls which can introduce latency jitter. It is best
/// used when threads can be bound to specific CPU cores.
/// </remarks>
public sealed class BusySpinWaitStrategy : IWaitStrategy, IIpcWaitStrategy
{
    public bool IsBlockingStrategy => false;

    public ISequenceWaiter NewSequenceWaiter(SequenceWaiterOwner owner, DependentSequenceGroup dependentSequences)
    {
        return new SequenceWaiter(dependentSequences);
    }

    public IIpcSequenceWaiter NewSequenceWaiter(SequenceWaiterOwner owner, IpcDependentSequenceGroup dependentSequences)
    {
        return new IpcSequenceWaiter(dependentSequences);
    }

    public void SignalAllWhenBlocking()
    {
    }

    private class SequenceWaiter(DependentSequenceGroup dependentSequences) : ISequenceWaiter
    {
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

    private class IpcSequenceWaiter(IpcDependentSequenceGroup dependentSequences) : IIpcSequenceWaiter
    {
        public SequenceWaitResult WaitFor(long sequence, CancellationToken cancellationToken)
        {
            long availableSequence;

            while ((availableSequence = dependentSequences.Value) < sequence)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return availableSequence;
        }
    }
}
