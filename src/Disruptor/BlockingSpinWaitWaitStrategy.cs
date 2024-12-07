using System;
using System.Threading;

namespace Disruptor;

/// <summary>
/// Blocking wait strategy that uses <c>Monitor.Wait</c> and <c>Monitor.PulseAll</c>.
/// </summary>
/// <remarks>
/// This strategy can be used when throughput and low-latency are not as important as CPU resources.
/// This strategy uses a <see cref="SpinWait"/> when waiting for the dependent sequence to prevent excessive CPU usage.
/// </remarks>
#pragma warning disable CS0618 // Type or member is obsolete
public sealed class BlockingSpinWaitWaitStrategy : ISequenceWaitStrategy, IWaitStrategy
#pragma warning restore CS0618 // Type or member is obsolete
{
    private readonly object _gate = new();

    public bool IsBlockingStrategy => true;

    public ISequenceWaiter NewSequenceWaiter(IEventHandler? eventHandler, DependentSequenceGroup dependentSequences)
    {
        return new SequenceWaiter(_gate, dependentSequences);
    }

    SequenceWaitResult IWaitStrategy.WaitFor(long sequence, DependentSequenceGroup dependentSequences, CancellationToken cancellationToken)
        => throw new NotSupportedException("IWaitStrategy must be converted to " + nameof(ISequenceWaitStrategy) + " before use.");

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
