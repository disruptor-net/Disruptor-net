using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// Blocking strategy that uses a lock and condition variable for <see cref="IEventProcessor"/>s waiting on a barrier.
    /// 
    /// This strategy should be used when performance and low-latency are not as important as CPU resource.
    /// </summary>
    public sealed class BlockingWaitStrategy : IWaitStrategy
    {
        private readonly object _gate = new object();

        /// <summary>
        /// Wait for the given sequence to be available
        /// </summary>
        /// <param name="sequence">sequence to be waited on.</param>
        /// <param name="cursor">Ring buffer cursor on which to wait.</param>
        /// <param name="dependentSequence">dependents further back the chain that must advance first</param>
        /// <param name="barrier">barrier the <see cref="IEventProcessor"/> is waiting on.</param>
        /// <returns>the sequence that is available which may be greater than the requested sequence.</returns>
        public long WaitFor(long sequence, Sequence cursor, ISequence dependentSequence, ISequenceBarrier barrier)
        {
            if (cursor.Value < sequence)
            {
                lock (_gate)
                {
                    while (cursor.Value < sequence)
                    {
                        barrier.CheckAlert();
                        Monitor.Wait(_gate);
                    }
                }
            }

            long availableSequence;
            while ((availableSequence = dependentSequence.Value) < sequence)
            {
                barrier.CheckAlert();
            }

            return availableSequence;
        }

        /// <summary>
        /// Signal those <see cref="IEventProcessor"/> waiting that the cursor has advanced.
        /// </summary>
        public void SignalAllWhenBlocking()
        {
            lock (_gate)
            {
                Monitor.PulseAll(_gate);
            }
        }
    }
}