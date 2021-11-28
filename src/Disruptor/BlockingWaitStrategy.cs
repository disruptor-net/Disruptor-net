using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// Blocking strategy that uses <c>Monitor.Wait</c> for event processors waiting on a barrier.
    /// </summary>
    /// <remarks>
    /// This strategy can be used when throughput and low-latency are not as important as CPU resource.
    /// This strategy busy spins when waiting for the dependent sequence, which can generate CPU spikes.
    ///
    /// Consider using <see cref="BlockingSpinWaitWaitStrategy"/> to avoid CPU spikes.
    /// </remarks>
    public sealed class BlockingWaitStrategy : IWaitStrategy
    {
        private readonly object _gate = new object();

        public long WaitFor(long sequence, Sequence cursor, ISequence dependentSequence, CancellationToken cancellationToken)
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

            var aggressiveSpinWait = new AggressiveSpinWait();
            long availableSequence;
            while ((availableSequence = dependentSequence.Value) < sequence)
            {
                cancellationToken.ThrowIfCancellationRequested();
                aggressiveSpinWait.SpinOnce();
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
}
