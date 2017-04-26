using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// Sleeping strategy that initially spins, then uses a Thread.Yield(), and
    /// eventually sleep(<code>Thread.Sleep(0)</code>) for the minimum
    /// number of nanos the OS and JVM will allow while the
    /// <see cref="IEventProcessor"/>s are waiting on a barrier.
    /// <p>
    /// This strategy is a good compromise between performance and CPU resource.
    /// Latency spikes can occur after quiet periods.
    /// </p>
    /// </summary>
    public sealed class SleepingWaitStrategy : IWaitStrategy
    {
        private const int _defaultRetries = 200;
        private readonly int _retries;

        public SleepingWaitStrategy(int retries = _defaultRetries)
        {
            _retries = retries;
        }

        /// <summary>
        /// <see cref="IWaitStrategy.WaitFor"/>
        /// </summary>
        public long WaitFor(long sequence, Sequence cursor, ISequence dependentSequence, ISequenceBarrier barrier)
        {
            long availableSequence;
            int counter = _retries;

            while ((availableSequence = dependentSequence.Value) < sequence)
            {
                counter = ApplyWaitMethod(barrier, counter);
            }

            return availableSequence;
        }

        /// <summary>
        /// <see cref="IWaitStrategy.SignalAllWhenBlocking"/>
        /// </summary>
        public void SignalAllWhenBlocking()
        {
        }

        private static int ApplyWaitMethod(ISequenceBarrier barrier, int counter)
        {
            barrier.CheckAlert();

            if (counter > 100)
            {
                --counter;
            }
#if NETSTANDARD2_0
            else if (counter > 0)
            {
                --counter;
                Thread.Yield();
            }
#endif
            else
            {
                Thread.Sleep(0);
            }

            return counter;
        }
    }
}