using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// Sleeping strategy that initially spins, then uses a <see cref="Thread.Sleep(int)"/>, and
    /// eventually sleep(<see cref="SpinWait"/>) for the minimum
    /// number of nanos the OS and JVM will allow while the
    /// {@link com.lmax.disruptor.EventProcessor}
    /// s are waiting on a barrier.
    /// <para/>
    /// This strategy is a good compromise between performance and CPU resource.
    /// Latency spikes can occur after quiet periods.
    /// </summary>
    public sealed class SleepingWaitStrategy : IWaitStrategy
    {
        private const int _defaultRetries = 200;
        private readonly int _retries;

        public SleepingWaitStrategy()
            : this(_defaultRetries)
        {
        }

        public SleepingWaitStrategy(int retries)
        {
            _retries = retries;
        }

        /// <summary>
        /// Wait for the given sequence to be available
        /// </summary>
        /// <param name="sequence">sequence to be waited on.</param>
        /// <param name="cursor">Ring buffer cursor on which to wait.</param>
        /// <param name="dependentSequence">dependents further back the chain that must advance first</param>
        /// <param name="barrier">barrier the <see cref="IEventProcessor"/> is waiting on.</param>
        /// <returns>the sequence that is available which may be greater than the requested sequence.</returns>
        public long WaitFor(long sequence, Sequence cursor, Sequence dependentSequence, ISequenceBarrier barrier)
        {
            var spinWait = default(SpinWait);
            long availableSequence;
            int counter = _retries;

            while ((availableSequence = dependentSequence.Value) < sequence)
            {
                counter = ApplyWaitMethod(barrier, counter, spinWait);
            }

            return availableSequence;
        }

        /// <summary>
        /// Signal those <see cref="IEventProcessor"/> waiting that the cursor has advanced.
        /// </summary>
        public void SignalAllWhenBlocking()
        {
        }

        private static int ApplyWaitMethod(ISequenceBarrier barrier, int counter, SpinWait spinWait)
        {
            barrier.CheckAlert();

            if (counter > 100)
            {
                --counter;
            }
            else if (counter > 0)
            {
                --counter;
                Thread.Sleep(0);
            }
            else
            {
                spinWait.SpinOnce(); // LockSupport.parkNanos(1L);
            }

            return counter;
        }
    }
}