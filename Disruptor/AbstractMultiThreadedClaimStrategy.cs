using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// An abstract base class for MultiThreaded Claim Strategies
    /// </summary>
    public abstract class AbstractMultiThreadedClaimStrategy : IClaimStrategy
    {
        /// <summary>
        /// The buffer size
        /// </summary>
        protected int _bufferSize;
        private readonly Sequence _claimSequence = new Sequence();
        private readonly ThreadLocal<MutableLong> _minGatingSequenceThreadLocal = new ThreadLocal<MutableLong>(() => new MutableLong(Sequencer.InitialCursorValue));

        /// <summary>
        /// Initializes a new instance of the <see cref="AbstractMultiThreadedClaimStrategy"/> class.
        /// </summary>
        /// <param name="bufferSize">Size of the buffer.</param>
        public AbstractMultiThreadedClaimStrategy(int bufferSize)
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
            return HasAvailableCapacity(_claimSequence.Value, availableCapacity, dependentSequences);
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

            long nextSequence = _claimSequence.IncrementAndGet();
            WaitForFreeSlotAt(nextSequence, dependentSequences, minGatingSequence);

            return nextSequence;
        }   

        /// <summary>
        /// Atomically checks the available capacity of the ring buffer and claims the next sequence.  Will
        /// throw InsufficientCapacityException if the capacity not available.
        /// </summary>
        /// <param name="availableCapacity">the capacity that should be available before claiming the next slot</param>
        /// <param name="delta">the number of slots to claim</param>
        /// <param name="dependentSequences">the set of sequences to check to ensure capacity is available</param>
        /// <returns>the index to be used for the publishing.</returns>
        public long CheckAndIncrement(int availableCapacity, int delta, Sequence[] dependentSequences)
        {
            for (;;)
            {
                long sequence = _claimSequence.Value;
                if (HasAvailableCapacity(sequence, availableCapacity, dependentSequences))
                {
                    long nextSequence = sequence + delta;
                    if (_claimSequence.CompareAndSet(sequence, nextSequence))
                    {
                        return nextSequence;
                    }
                }
                else
                {
                    throw InsufficientCapacityException.Instance;
                }
            }
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
            long nextSequence = _claimSequence.AddAndGet(delta);
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
            _claimSequence.Value = sequence;
            WaitForFreeSlotAt(sequence, dependentSequences, _minGatingSequenceThreadLocal.Value);
        }

        /// <summary>
        /// Serialize publishers in sequence and set cursor to latest available sequence.
        /// </summary>
        /// <param name="sequence">sequence to be applied</param>
        /// <param name="cursor">cursor to serialize against.</param>
        /// <param name="batchSize">batchSize of the sequence.</param>
        public abstract void SerialisePublishing(long sequence, Sequence cursor, long batchSize);

        private void WaitForCapacity(Sequence[] dependentSequences, MutableLong minGatingSequence)
        {
            long wrapPoint = (_claimSequence.Value + 1L) - _bufferSize;
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

        private bool HasAvailableCapacity(long sequence, int availableCapacity, Sequence[] dependentSequences)
        {
            long wrapPoint = (sequence + availableCapacity) - _bufferSize;
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
    }
}