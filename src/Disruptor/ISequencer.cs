namespace Disruptor
{
    /// <summary>
    /// Coordinator for claiming sequences for access to a data structure while tracking dependent <see cref="Sequence"/>s
    /// </summary>
    public interface ISequencer : ISequenced, ICursored
    {
        /// <summary>
        /// Claim a specific sequence when only one publisher is involved.
        /// </summary>
        /// <param name="sequence">sequence to be claimed.</param>
        void Claim(long sequence);

        /// <summary>
        /// Confirms if a sequence is published and the event is available for use; non-blocking.
        /// </summary>
        /// <param name="sequence">sequence of the buffer to check</param>
        /// <returns>true if the sequence is available for use, false if not</returns>
        bool IsAvailable(long sequence);

        /// <summary>
        /// Add the specified gating sequences to this instance of the Disruptor.  They will
        /// safely and atomically added to the list of gating sequences.
        /// </summary>
        /// <param name="gatingSequences">The sequences to add.</param>
        void AddGatingSequences(params ISequence[] gatingSequences);

        /// <summary>
        /// Remove the specified sequence from this sequencer.
        /// </summary>
        /// <param name="sequence">to be removed.</param>
        /// <returns>true if this sequence was found, false otherwise.</returns>
        bool RemoveGatingSequence(ISequence sequence);

        /// <summary>
        /// Create a <see cref="ISequenceBarrier"/> that gates on the the cursor and a list of <see cref="Sequence"/>s
        /// </summary>
        /// <param name="sequencesToTrack">All of the sequences that the newly constructed barrier will wait on.</param>
        /// <returns>A sequence barrier that will track the specified sequences.</returns>
        ISequenceBarrier NewBarrier(params ISequence[] sequencesToTrack);

        /// <summary>
        /// Get the minimum sequence value from all of the gating sequences
        /// added to this ringBuffer.
        /// </summary>
        /// <returns>The minimum gating sequence or the cursor sequence if no sequences have been added.</returns>
        long GetMinimumSequence();

        /// <summary>
        /// Get the highest sequence number that can be safely read from the ring buffer.  Depending
        /// on the implementation of the Sequencer this call may need to scan a number of values
        /// in the Sequencer.  The scan will range from nextSequence to availableSequence.  If
        /// there are no available values <code>>= nextSequence</code> the return value will be
        /// <code>nextSequence - 1</code>.  To work correctly a consumer should pass a value that
        /// it 1 higher than the last sequence that was successfully processed.
        /// </summary>
        /// <param name="nextSequence">The sequence to start scanning from.</param>
        /// <param name="availableSequence">The sequence to scan to.</param>
        /// <returns>The highest value that can be safely read, will be at least <code>nextSequence - 1</code>.</returns>
        long GetHighestPublishedSequence(long nextSequence, long availableSequence);

        /// <summary>
        /// Creates an event poller for this sequence that will use the supplied data provider and
        /// gating sequences.
        /// </summary>
        EventPoller<T> NewPoller<T>(IDataProvider<T> provider, params ISequence[] gatingSequences)
            where T : class;

        /// <summary>
        /// Creates an event poller for this sequence that will use the supplied data provider and
        /// gating sequences.
        /// </summary>
        ValueEventPoller<T> NewPoller<T>(IValueDataProvider<T> provider, params ISequence[] gatingSequences)
            where T : struct;
    }
}
