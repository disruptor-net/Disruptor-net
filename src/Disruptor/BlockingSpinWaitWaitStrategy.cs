using System.Threading;

namespace Disruptor;

/// <summary>
/// Blocking strategy that uses <c>Monitor.Wait</c> for event processors waiting on a barrier.
/// </summary>
/// <remarks>
/// This strategy can be used when throughput and low-latency are not as important as CPU resources.
/// This strategy uses a <see cref="SpinWait"/> when waiting for the dependent sequence to prevent excessive CPU usage.
/// </remarks>
public sealed class BlockingSpinWaitWaitStrategy : IWaitStrategy
{
    private readonly object _gate = new();

    public bool IsBlockingStrategy => true;

    public SequenceWaitResult WaitFor(long sequence, Sequence cursor, ISequence dependentSequence, CancellationToken cancellationToken)
    {
        if (cursor.Value < sequence)
        {
            lock (_gate)
            {
                while (cursor.Value < sequence)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Monitor.Wait(_gate);
                }
            }
        }

        var spinWait = new SpinWait();
        long availableSequence;
        while ((availableSequence = dependentSequence.Value) < sequence)
        {
            cancellationToken.ThrowIfCancellationRequested();
            spinWait.SpinOnce();
        }

        return availableSequence;
    }

    public void SignalAllWhenBlocking()
    {
        lock (_gate)
        {
            Monitor.PulseAll(_gate);
        }
    }
}
