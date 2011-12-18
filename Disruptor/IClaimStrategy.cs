namespace Disruptor
{
    /// <summary>
    /// Strategy contract for claiming the sequence of events in the <see cref="Sequencer"/> by event publishers.
    /// </summary>
    public interface IClaimStrategy
    {
        /// <summary>
        /// Get the size of the data structure used to buffer events.
        /// </summary>
        int BufferSize { get; }

        /// <summary>
        /// Get the current claimed sequence.
        /// </summary>
        long Sequence { get; }

        /// <summary>
        /// Is there available capacity in the buffer for the requested sequence.
        /// </summary>
        /// <param name="availableCapacity">availableCapacity remaining in the buffer.</param>
        /// <param name="dependentSequences">dependentSequences to be checked for range.</param>
        /// <returns>true if the buffer has capacity for the requested sequence.</returns>
        bool HasAvailableCapacity(int availableCapacity, Sequence[] dependentSequences);

        /// <summary>
        /// Claim the next sequence in the <see cref="Sequencer"/>
        /// The caller should be held up until the claimed sequence is available by tracking the dependentSequences.
        /// </summary>
        /// <param name="dependentSequences">dependentSequences to be checked for range.</param>
        /// <returns>the index to be used for the publishing.</returns>
        long IncrementAndGet(Sequence[] dependentSequences);

        ///<summary>
        /// Increment sequence by a delta and get the result.
        /// The caller should be held up until the claimed sequence batch is available by tracking the dependentSequences.
        ///</summary>
        ///<param name="delta">delta to increment by.</param>
        /// <param name="dependentSequences">dependentSequences to be checked for range.</param>
        ///<returns>the result after incrementing.</returns>
        long IncrementAndGet(int delta, Sequence[] dependentSequences);

        /// <summary>
        /// Set the current sequence value for claiming an event in the <see cref="Sequencer"/>
        /// The caller should be held up until the claimed sequence is available by tracking the dependentSequences.
        /// </summary>
        /// <param name="sequence">sequence to be set as the current value.</param>
        /// <param name="dependentSequences">dependentSequences to be checked for range.</param>
        void SetSequence(long sequence, Sequence[] dependentSequences);

        ///<summary>
        /// Serialise publishers in sequence and set cursor to latest available sequence.
        ///</summary>
        ///<param name="sequence">sequence to be applied</param>
        ///<param name="cursor">cursor to serialise against.</param>
        ///<param name="batchSize">batchSize of the sequence.</param>
        void SerialisePublishing(long sequence, Sequence cursor, long batchSize);
    }
}
