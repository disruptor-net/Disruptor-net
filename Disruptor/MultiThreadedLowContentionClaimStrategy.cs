using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// Strategy to be used when there are multiple producer threads claiming sequences.
    /// 
    /// This strategy requires sufficient cores to allow multiple publishers to be concurrently claiming sequences.
    /// </summary>
    public sealed class MultiThreadedLowContentionClaimStrategy : AbstractMultiThreadedClaimStrategy
    {
        private readonly int _bufferSize;
        private Volatile.PaddedLong _claimSequence = new Volatile.PaddedLong(Sequencer.InitialCursorValue);
        private readonly ThreadLocal<MutableLong> _minGatingSequenceThreadLocal;

        /// <summary>
        /// Construct a new multi-threaded publisher <see cref="IClaimStrategy"/> for a given buffer size.
        /// </summary>
        /// <param name="bufferSize">bufferSize for the underlying data structure.</param>
        public MultiThreadedLowContentionClaimStrategy(int bufferSize) : base(bufferSize)
        {
            _bufferSize = bufferSize;
            _minGatingSequenceThreadLocal = new ThreadLocal<MutableLong>(() => new MutableLong(Sequencer.InitialCursorValue));
        }

        ///<summary>
        /// Serialise publishers in sequence and set cursor to latest available sequence.
        ///</summary>
        ///<param name="sequence">sequence to be applied</param>
        ///<param name="cursor">cursor to serialise against.</param>
        ///<param name="batchSize">batchSize of the sequence.</param>
        public override void SerialisePublishing(long sequence, Sequence cursor, long batchSize)
        {
            long expectedSequence = sequence - batchSize;
            while (expectedSequence != cursor.Value)
            {
                // busy spin
            }

            cursor.LazySet(sequence);
        }
    }
}
