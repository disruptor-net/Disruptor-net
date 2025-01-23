using System;
using System.Threading;

namespace Disruptor;

/// <summary>
/// Non-blocking wait strategy that uses <c>Thread.Yield()</c>.
/// </summary>
/// <remarks>
/// This strategy is a good compromise between performance and CPU resources without incurring significant latency spikes.
/// </remarks>
#pragma warning disable CS0618 // Type or member is obsolete
public sealed class YieldingWaitStrategy : ISequenceWaitStrategy, IWaitStrategy
#pragma warning restore CS0618 // Type or member is obsolete
{
    private readonly int _yieldIndex;

    public YieldingWaitStrategy()
        : this(100)
    {
    }

    public YieldingWaitStrategy(int busySpinCount)
    {
        _yieldIndex = busySpinCount;
    }

    public bool IsBlockingStrategy => false;

    public ISequenceWaiter NewSequenceWaiter(SequenceWaiterOwner owner, DependentSequenceGroup dependentSequences)
    {
        return new SequenceWaiter(dependentSequences, _yieldIndex);
    }

    public SequenceWaitResult WaitFor(long sequence, DependentSequenceGroup dependentSequences, CancellationToken cancellationToken)
        => throw new NotSupportedException("IWaitStrategy must be converted to " + nameof(ISequenceWaitStrategy) + " before use.");

    public void SignalAllWhenBlocking()
    {
    }

    private class SequenceWaiter(DependentSequenceGroup dependentSequences, int yieldIndex) : ISequenceWaiter
    {
        public DependentSequenceGroup DependentSequences => dependentSequences;

        public SequenceWaitResult WaitFor(long sequence, CancellationToken cancellationToken)
        {
            long availableSequence;
            var counter = 0;

            while ((availableSequence = dependentSequences.Value) < sequence)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (counter >= yieldIndex)
                {
                    Thread.Yield();
                }

                counter++;
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
