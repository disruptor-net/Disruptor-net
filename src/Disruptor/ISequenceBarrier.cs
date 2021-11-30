using System;
using System.Threading;
using Disruptor.Processing;

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
        /// <exception cref="OperationCanceledException">if a status change has occurred for the Disruptor</exception>
        SequenceWaitResult WaitFor(long sequence);

        /// <summary>
        /// Delegate a call to the <see cref="ISequencer"/>.
        /// Returns the value of the cursor for events that have been published.
        /// </summary>
        long Cursor { get; }

        /// <summary>
        /// The cancellation token used to stop the processing.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// Resets the <see cref="CancellationToken"/>.
        /// </summary>
        void ResetProcessing();

        /// <summary>
        /// Cancels the <see cref="CancellationToken"/>.
        /// </summary>
        void CancelProcessing();
    }
}
