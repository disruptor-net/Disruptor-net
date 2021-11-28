using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// Sleeping strategy that initially spins, then uses <c>Thread.Yield()</c>, and
    /// eventually <c>Thread.Sleep(0)</c> while the event processors
    /// are waiting on a barrier.
    /// </summary>
    /// <remarks>
    /// This strategy is a good compromise between performance and CPU resource.
    /// Latency spikes can occur after quiet periods.  It will also reduce the impact
    /// on the producing thread as it will not need signal any conditional variables
    /// to wake up the event handling thread.
    /// </remarks>
    public sealed class SleepingWaitStrategy : INonBlockingWaitStrategy
    {
        private const int _defaultRetries = 200;
        private readonly int _retries;

        public SleepingWaitStrategy(int retries = _defaultRetries)
        {
            _retries = retries;
        }

        public long WaitFor(long sequence, Sequence cursor, ISequence dependentSequence, CancellationToken cancellationToken)
        {
            long availableSequence;
            int counter = _retries;

            while ((availableSequence = dependentSequence.Value) < sequence)
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

            if (counter > 100)
            {
                --counter;
            }
            else if (counter > 0)
            {
                --counter;
                Thread.Yield();
            }
            else
            {
                Thread.Sleep(0);
            }

            return counter;
        }
    }
}
