using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// Optimised strategy can be used when there is a single publisher thread claiming sequences.
    /// 
    /// This strategy must <b>not</b> be used when multiple threads are used for publishing concurrently on the same {@link Sequencer}
    /// </summary>
    public sealed class SingleThreadedClaimStrategy : IClaimStrategy
    {
        private readonly int _bufferSize;
        private Volatile.PaddedLong _claimSequence = new Volatile.PaddedLong(Sequencer.InitialCursorValue);
        private Volatile.PaddedLong _minGatingSequence = new Volatile.PaddedLong(Sequencer.InitialCursorValue);

        /// <summary>
        /// Construct a new single threaded publisher <see cref="IClaimStrategy"/> for a given buffer size.
        /// </summary>
        /// <param name="bufferSize">bufferSize for the underlying data structure.</param>
        public SingleThreadedClaimStrategy(int bufferSize)
        {
            _bufferSize = bufferSize;
        }

        /// <summary>
        /// Get the size of the data structure used to buffer events.
        /// </summary>
        public int BufferSize
        {
            get { return _bufferSize; }
        }

        /// <summary>
        /// Get the current claimed sequence.
        /// </summary>
        public long Sequence
        {
            get { return _claimSequence.ReadUnfenced(); }
        }

        /// <summary>
        /// Is there available capacity in the buffer for the requested sequence.
        /// </summary>
        /// <param name="availableCapacity">availableCapacity remaining in the buffer.</param>
        /// <param name="dependentSequences">dependentSequences to be checked for range.</param>
        /// <returns>true if the buffer has capacity for the requested sequence.</returns>
        public bool HasAvailableCapacity(int availableCapacity, Sequence[] dependentSequences)
        {
            long wrapPoint = (_claimSequence.ReadUnfenced() + availableCapacity) - _bufferSize;
            if (wrapPoint > _minGatingSequence.ReadUnfenced())
            {
                long minSequence = Util.GetMinimumSequence(dependentSequences);
                _minGatingSequence.WriteUnfenced(minSequence);

                if (wrapPoint > minSequence)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Claim the next sequence in the <see cref="Sequencer"/>
        /// The caller should be held up until the claimed sequence is available by tracking the dependentSequences.
        /// </summary>
        /// <param name="dependentSequences">dependentSequences to be checked for range.</param>
        /// <returns>the index to be used for the publishing.</returns>
        public long IncrementAndGet(Sequence[] dependentSequences)
        {
            long nextSequence = _claimSequence.ReadUnfenced() + 1L;
            _claimSequence.WriteUnfenced(nextSequence);
            WaitForFreeSlotAt(nextSequence, dependentSequences);

            return nextSequence;
        }

        ///<summary>
        /// Increment sequence by a delta and get the result.
        /// The caller should be held up until the claimed sequence batch is available by tracking the dependentSequences.
        ///</summary>
        ///<param name="delta">delta to increment by.</param>
        /// <param name="dependentSequences">dependentSequences to be checked for range.</param>
        ///<returns>the result after incrementing.</returns>
        public long IncrementAndGet(int delta, Sequence[] dependentSequences)
        {
            long nextSequence = _claimSequence.ReadUnfenced() + delta;
            _claimSequence.WriteUnfenced(nextSequence);
            WaitForFreeSlotAt(nextSequence, dependentSequences);

            return nextSequence;
        }

        /// <summary>
        /// Set the current sequence value for claiming an event in the <see cref="Sequencer"/>
        /// The caller should be held up until the claimed sequence is available by tracking the dependentSequences.
        /// </summary>
        /// <param name="sequence">sequence to be set as the current value.</param>
        /// <param name="dependentSequences">dependentSequences to be checked for range.</param>
        public void SetSequence(long sequence, Sequence[] dependentSequences)
        {
            _claimSequence.WriteUnfenced(sequence);
            WaitForFreeSlotAt(sequence, dependentSequences);
        }

        ///<summary>
        /// Serialise publishers in sequence and set cursor to latest available sequence.
        ///</summary>
        ///<param name="sequence">sequence to be applied</param>
        ///<param name="cursor">cursor to serialise against.</param>
        ///<param name="batchSize">batchSize of the sequence.</param>
        public void SerialisePublishing(long sequence, Sequence cursor, long batchSize)
        {
            cursor.LazySet(sequence);
        }

        private void WaitForFreeSlotAt(long sequence, Sequence[] dependentSequences)
        {
            long wrapPoint = sequence - _bufferSize;
            if (wrapPoint > _minGatingSequence.ReadUnfenced())
            {
                long minSequence;
                var spinWait = default(SpinWait);
                while (wrapPoint > (minSequence = Util.GetMinimumSequence(dependentSequences)))
                {
                    spinWait.SpinOnce(); // LockSupport.parkNanos(1L);
                }

                _minGatingSequence.WriteUnfenced(minSequence);
            }
        }
    }
}