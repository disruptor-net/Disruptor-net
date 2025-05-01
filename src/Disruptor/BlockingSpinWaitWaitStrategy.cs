using System.Threading;

namespace Disruptor;

/// <summary>
/// Blocking wait strategy that uses <c>Monitor.Wait</c> and <c>Monitor.PulseAll</c>.
/// </summary>
/// <remarks>
/// This strategy can be used when throughput and low-latency are not as important as CPU resources.
/// This strategy uses a <see cref="SpinWait"/> when waiting for the dependent sequence to prevent excessive CPU usage.
/// </remarks>
public sealed class BlockingSpinWaitWaitStrategy : IWaitStrategy
{
    private readonly object _gate = new();

    public bool IsBlockingStrategy => true;

    public ISequenceWaiter NewSequenceWaiter(IEventHandler? eventHandler, DependentSequenceGroup dependentSequences)
    {
        return new SequenceWaiter(_gate, dependentSequences);
    }

    public void SignalAllWhenBlocking()
    {
        lock (_gate)
        {
            Monitor.PulseAll(_gate);
        }
    }

    private class SequenceWaiter(object gate, DependentSequenceGroup dependentSequences) : ISequenceWaiter
    {
        public DependentSequenceGroup DependentSequences => dependentSequences;

        public SequenceWaitResult WaitFor(long sequence, CancellationToken cancellationToken)
        {
            if (dependentSequences.CursorValue < sequence)
            {
                lock (gate)
                {
                    while (dependentSequences.CursorValue < sequence)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Monitor.Wait(gate);
                    }
                }
            }

            return dependentSequences.SpinWaitFor(sequence, cancellationToken);
        }

        public void Cancel()
        {
            lock (gate)
            {
                Monitor.PulseAll(gate);
            }
        }

        public void Dispose()
        {
        }
    }
}
