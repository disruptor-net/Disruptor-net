using System.Threading;
using Disruptor.MemoryLayout;

namespace Disruptor
{
    /// <summary>
    /// Strategy to be used when there are multiple producer threads claiming sequences.
    /// 
    /// This strategy requires sufficient cores to allow multiple publishers to be concurrently claiming sequences.
    /// </summary>
    public sealed class MultiThreadedClaimStrategy : IClaimStrategy
    {
        private readonly int _bufferSize;
        private PaddedAtomicLong _claimSequence = new PaddedAtomicLong(Sequencer.InitialCursorValue);
        private readonly ThreadLocal<MutableLong> _minGatingSequenceThreadLocal;

        /// <summary>
        /// Construct a new multi-threaded publisher <see cref="IClaimStrategy"/> for a given buffer size.
        /// </summary>
        /// <param name="bufferSize">bufferSize for the underlying data structure.</param>
        public MultiThreadedClaimStrategy(int bufferSize)
        {
            _bufferSize = bufferSize;
            _minGatingSequenceThreadLocal = new ThreadLocal<MutableLong>(() => new MutableLong(Sequencer.InitialCursorValue));
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
            get { return _claimSequence.Value; }
        }

        /// <summary>
        /// Is there available capacity in the buffer for the requested sequence.
        /// </summary>
        /// <param name="availableCapacity">availableCapacity remaining in the buffer.</param>
        /// <param name="dependentSequences">dependentSequences to be checked for range.</param>
        /// <returns>true if the buffer has capacity for the requested sequence.</returns>
        public bool HasAvailableCapacity(int availableCapacity, Sequence[] dependentSequences)
        {
            long wrapPoint = (_claimSequence.Value + availableCapacity) - _bufferSize;
            var minGatingSequence = _minGatingSequenceThreadLocal.Value;
            if (wrapPoint > minGatingSequence.Value)
            {
                long minSequence = Util.GetMinimumSequence(dependentSequences);
                minGatingSequence.Value = minSequence;

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
            var minGatingSequence = _minGatingSequenceThreadLocal.Value;
            WaitForCapacity(dependentSequences, minGatingSequence);

            long nextSequence = _claimSequence.IncrementAndGet();
            WaitForFreeSlotAt(nextSequence, dependentSequences, minGatingSequence);

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
            long nextSequence = _claimSequence.IncrementAndGet(delta);
            WaitForFreeSlotAt(nextSequence, dependentSequences, _minGatingSequenceThreadLocal.Value);

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
            _claimSequence.LazySet(sequence);
            WaitForFreeSlotAt(sequence, dependentSequences, _minGatingSequenceThreadLocal.Value);
        }

        ///<summary>
        /// Serialise publishers in sequence and set cursor to latest available sequence.
        ///</summary>
        ///<param name="sequence">sequence to be applied</param>
        ///<param name="cursor">cursor to serialise against.</param>
        ///<param name="batchSize">batchSize of the sequence.</param>
        public void SerialisePublishing(long sequence, Sequence cursor, long batchSize)
        {
            long expectedSequence = sequence - batchSize;
            while (expectedSequence != cursor.Value)
            {
                // busy spin
            }

            cursor.LazySet(sequence);
        }

        private void WaitForCapacity(Sequence[] dependentSequences, MutableLong minGatingSequence)
        {
            long wrapPoint = (_claimSequence.Value + 1L) - _bufferSize;
            if (wrapPoint > minGatingSequence.Value)
            {
                long minSequence;
                while (wrapPoint > (minSequence = Util.GetMinimumSequence(dependentSequences)))
                {
                    //TODO LockSupport.parkNanos(1L);
                }

                minGatingSequence.Value = minSequence;
            }
        }

        private void WaitForFreeSlotAt(long sequence, Sequence[] dependentSequences, MutableLong minGatingSequence)
        {
            long wrapPoint = sequence - _bufferSize;
            if (wrapPoint > minGatingSequence.Value)
            {
                long minSequence;
                while (wrapPoint > (minSequence = Util.GetMinimumSequence(dependentSequences)))
                {
                    //TODO LockSupport.parkNanos(1L);
                }

                minGatingSequence.Value =  minSequence;
            }

        }

        /// <summary>
        /// Holder class for a long value.
        /// </summary>
        private class MutableLong
        {
            public long Value { get; set; }

            public MutableLong(long value)
            {
                Value = value;
            }
        }
    }
}
