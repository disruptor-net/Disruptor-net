using System;
using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// Strategy to be used when there are multiple publisher threads claiming sequences.
    /// 
    /// This strategy is reasonably forgiving when the multiple publisher threads are highly contended or working in an
    /// environment where there is insufficient CPUs to handle multiple publisher threads.  It requires 2 CAS operations
    /// for a single publisher, compared to the <see cref="MultiThreadedLowContentionClaimStrategy"/> strategy which needs only a single CAS and a
    /// lazySet per publication.
    /// </summary>
    public class MultiThreadedClaimStrategy : IClaimStrategy
    {
        private readonly int _bufferSize;
        private Volatile.PaddedLong _claimSequence = new Volatile.PaddedLong(Sequencer.InitialCursorValue);
        private readonly Volatile.LongArray _pendingPublication;
        private readonly int _pendingMask;
        private readonly ThreadLocal<MutableLong> _minGatingSequenceThreadLocal = new ThreadLocal<MutableLong>(() => new MutableLong(Sequencer.InitialCursorValue));

        /// <summary>
        /// Construct a new multi-threaded publisher <see cref="IClaimStrategy"/> for a given buffer size.
        /// </summary>
        /// <param name="bufferSize">bufferSize for the underlying data structure.</param>
        /// <param name="pendingBufferSize">pendingBufferSize number of item that can be pending for serialization</param>
        public MultiThreadedClaimStrategy(int bufferSize, int pendingBufferSize = 1024)
        {
            if (!pendingBufferSize.IsPowerOf2())
            {
                throw new ArgumentException("must be power of 2", "pendingBufferSize");
            }
            _bufferSize = bufferSize;
            _pendingPublication = new Volatile.LongArray(pendingBufferSize);
            _pendingMask = pendingBufferSize - 1;
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
            get { return _claimSequence.ReadFullFence(); }
        }

        /// <summary>
        /// Is there available capacity in the buffer for the requested sequence.
        /// </summary>
        /// <param name="availableCapacity">availableCapacity remaining in the buffer.</param>
        /// <param name="dependentSequences">dependentSequences to be checked for range.</param>
        /// <returns>true if the buffer has capacity for the requested sequence.</returns>
        public bool HasAvailableCapacity(int availableCapacity, Sequence[] dependentSequences)
        {
            long wrapPoint = (_claimSequence.ReadFullFence() + availableCapacity) - _bufferSize;
            MutableLong minGatingSequence = _minGatingSequenceThreadLocal.Value;
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
            MutableLong minGatingSequence = _minGatingSequenceThreadLocal.Value;
            WaitForCapacity(dependentSequences, minGatingSequence);

            long nextSequence = _claimSequence.AtomicIncrementAndGet();
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
            long nextSequence = _claimSequence.AtomicAddAndGet(delta);
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
            _claimSequence.WriteCompilerOnlyFence(sequence);
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
            var spinWait = default(SpinWait);
            while (sequence - cursor.Value > _pendingPublication.Length)
            {
                spinWait.SpinOnce();
            }

            long expectedSequence = sequence - batchSize;
            for (long pendingSequence = expectedSequence + 1; pendingSequence <= sequence; pendingSequence++)
            {
                _pendingPublication.WriteFullFence((int)pendingSequence & _pendingMask, pendingSequence);
            }

            long cursorSequence = cursor.Value;
            if (cursorSequence >= sequence)
            {
                return;
            }
            expectedSequence = Math.Max(expectedSequence, cursorSequence);
            long nextSequence = expectedSequence + 1;
            while (cursor.CompareAndSet(expectedSequence, nextSequence))
            {
                expectedSequence = nextSequence;
                nextSequence++;
                if (_pendingPublication.ReadFullFence((int)nextSequence & _pendingMask) != nextSequence)
                {
                    break;
                }
            }
        }

        private void WaitForCapacity(Sequence[] dependentSequences, MutableLong minGatingSequence)
        {
            long wrapPoint = (_claimSequence.ReadFullFence() + 1L) - _bufferSize;
            if (wrapPoint > minGatingSequence.Value)
            {
                var spinWait = default(SpinWait);
                long minSequence;
                while (wrapPoint > (minSequence = Util.GetMinimumSequence(dependentSequences)))
                {
                    spinWait.SpinOnce(); //Java version uses LockSupport.parkNanos(1L);
                }

                minGatingSequence.Value = minSequence;
            }
        }

        private void WaitForFreeSlotAt(long sequence, Sequence[] dependentSequences, MutableLong minGatingSequence)
        {
            long wrapPoint = sequence - _bufferSize;
            if (wrapPoint > minGatingSequence.Value)
            {
                var spinWait = default(SpinWait);
                long minSequence;
                while (wrapPoint > (minSequence = Util.GetMinimumSequence(dependentSequences)))
                {
                    spinWait.SpinOnce(); //Java version uses LockSupport.parkNanos(1L);
                }

                minGatingSequence.Value = minSequence;
            }
        }

    }
}