using System.Threading;

namespace Disruptor;

/// <summary>
/// Yielding strategy that uses <c>Thread.Yield()</c> for event processors waiting on a barrier
/// after an initially spinning.
/// </summary>
/// <remarks>
/// This strategy is a good compromise between performance and CPU resources without incurring significant latency spikes.
/// </remarks>
public sealed class YieldingWaitStrategy : IWaitStrategy
{
    private const int _spinTries = 100;

    public bool IsBlockingStrategy => false;

    public SequenceWaitResult WaitFor(long sequence, DependentSequenceGroup dependentSequences, CancellationToken cancellationToken)
    {
        long availableSequence;
        var counter = _spinTries;

        while ((availableSequence = dependentSequences.Value) < sequence)
        {
            counter = ApplyWaitMethod(cancellationToken, counter);
        }

        return availableSequence;
    }

    public void SignalAllWhenBlocking()
    {
    }

    private static int ApplyWaitMethod(CancellationToken cancellationToken, int counter)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if(counter == 0)
        {
            Thread.Yield();
        }
        else
        {
            --counter;
        }

        return counter;
    }
}
