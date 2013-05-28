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
    public class MultiThreadedClaimStrategy : AbstractMultiThreadedClaimStrategy
    {
        private readonly Volatile.LongArray _pendingPublication;
        private readonly int _pendingMask;

        /// <summary>
        /// Construct a new multi-threaded publisher <see cref="IClaimStrategy"/> for a given buffer size.
        /// </summary>
        /// <param name="bufferSize">bufferSize for the underlying data structure.</param>
        /// <param name="pendingBufferSize">pendingBufferSize number of item that can be pending for serialization</param>
        public MultiThreadedClaimStrategy(int bufferSize, int pendingBufferSize = 1024) : base(bufferSize)
        {
            
            if (!pendingBufferSize.IsPowerOf2())
            {
                throw new ArgumentException("must be power of 2", "pendingBufferSize");
            }
            _bufferSize = bufferSize;
            _pendingPublication = new Volatile.LongArray(pendingBufferSize);
            _pendingMask = pendingBufferSize - 1;
        }

        ///<summary>
        /// Serialise publishers in sequence and set cursor to latest available sequence.
        ///</summary>
        ///<param name="sequence">sequence to be applied</param>
        ///<param name="cursor">cursor to serialise against.</param>
        ///<param name="batchSize">batchSize of the sequence.</param>
        public override void SerialisePublishing(long sequence, Sequence cursor, long batchSize)
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
    }
}