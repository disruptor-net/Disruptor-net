namespace Disruptor
{
    /// <summary>
    /// Busy Spin strategy that uses a busy spin loop for <see cref="IEventProcessor"/>s waiting on a barrier.
    /// 
    /// This strategy will use CPU resource to avoid syscalls which can introduce latency jitter.  It is best
    /// used when threads can be bound to specific CPU cores.
    /// </summary>
    public sealed class BusySpinWaitStrategy : IWaitStrategy
    {
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
        }
    }
}