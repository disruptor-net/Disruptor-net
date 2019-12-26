namespace Disruptor
{
    /// <summary>
    /// Coordination barrier for tracking the cursor for producers and sequence of
    /// dependent <see cref="IEventProcessor"/>s for a <see cref="RingBuffer{T}"/>
    /// </summary>
    public interface ISequenceBarrier
    {
        /// <summary>
        /// Wait for the given sequence to be available for consumption.
        /// </summary>
        /// <param name="sequence">sequence to wait for</param>
        /// <returns>the sequence up to which is available</returns>
        /// <exception cref="AlertException">if a status change has occurred for the Disruptor</exception>
        /// <exception cref="TimeoutException">if a timeout occurs while waiting for the supplied sequence.</exception>
        long WaitFor(long sequence);

        /// <summary>
        /// Delegate a call to the <see cref="ISequencer"/>.
        /// Returns the value of the cursor for events that have been published.
        /// </summary>
        long Cursor { get; }

        /// <summary>
        /// The current alert status for the barrier.
        /// Returns true if in alert otherwise false.
        /// </summary>
        bool IsAlerted { get; }

        /// <summary>
        ///  Alert the <see cref="IEventProcessor"/> of a status change and stay in this status until cleared.
        /// </summary>
        void Alert();

        /// <summary>
        /// Clear the current alert status.
        /// </summary>
        void ClearAlert();

        /// <summary>
        /// Check if an alert has been raised and throw an <see cref="AlertException"/> if it has.
        /// </summary>
        /// <exception cref="AlertException">if alert has been raised.</exception>
        void CheckAlert();
    }
}

