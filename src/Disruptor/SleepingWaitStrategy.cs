using System.Threading;

namespace Disruptor;

/// <summary>
/// Non-blocking wait strategy that initially spins, then uses <c>Thread.Yield()</c>, and
/// eventually <c>Thread.Sleep(0)</c> while the event processors
/// are waiting on a barrier.
/// </summary>
/// <remarks>
/// This strategy is a good compromise between performance and CPU resources.
/// Latency spikes can occur after quiet periods.  It will also reduce the impact
/// on the producing thread as it will not need signal any conditional variables
/// to wake up the event handling thread.
/// </remarks>
public sealed class SleepingWaitStrategy : IWaitStrategy
{
    private readonly int _yieldIndex;
    private readonly int _sleepIndex;

    public SleepingWaitStrategy()
        : this(100, 100)
    {
    }

    public SleepingWaitStrategy(int busySpinCount, int yieldCount)
    {
        _yieldIndex = busySpinCount;
        _sleepIndex = busySpinCount + yieldCount;
    }

    public bool IsBlockingStrategy => false;

    public ISequenceWaiter NewSequenceWaiter(IEventHandler? eventHandler, DependentSequenceGroup dependentSequences)
    {
        return new SequenceWaiter(dependentSequences, _yieldIndex, _sleepIndex);
    }

    public void SignalAllWhenBlocking()
    {
    }

    private class SequenceWaiter(DependentSequenceGroup dependentSequences, int yieldIndex, int sleepIndex) : ISequenceWaiter
    {
        public DependentSequenceGroup DependentSequences => dependentSequences;

        public SequenceWaitResult WaitFor(long sequence, CancellationToken cancellationToken)
        {
            long availableSequence;
            var counter = 0;

            while ((availableSequence = dependentSequences.Value) < sequence)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (counter >= sleepIndex)
                {
                    Thread.Sleep(0);
                }
                else if (counter >= yieldIndex)
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
