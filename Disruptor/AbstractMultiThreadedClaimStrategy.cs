using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class AbstractMultiThreadedClaimStrategy : IClaimStrategy
    {
        /// <summary>
        /// 
        /// </summary>
        protected int _bufferSize;
        private Sequence _claimSequence = new Sequence();
        private readonly ThreadLocal<MutableLong> _minGatingSequenceThreadLocal = new ThreadLocal<MutableLong>(() => new MutableLong(Sequencer.InitialCursorValue));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bufferSize"></param>
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
        /// 
        /// </summary>
        /// <param name="availableCapacity"></param>
        /// <param name="delta"></param>
        /// <param name="dependentSequences"></param>
        /// <returns></returns>
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
        /// 
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="cursor"></param>
        /// <param name="batchSize"></param>
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