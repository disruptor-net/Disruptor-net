using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Disruptor
{
    public class SingleProducerSequencer : Sequencer
    {
        private Fields _fields = new Fields(Sequence.InitialCursorValue, Sequence.InitialCursorValue);

        public SingleProducerSequencer(int bufferSize, IWaitStrategy waitStrategy)
            : base(bufferSize, waitStrategy)
        {
        }

        /// <summary>
        /// Has the buffer got capacity to allocate another sequence.  This is a concurrent
        /// method so the response should only be taken as an indication of available capacity.
        /// </summary>
        /// <param name="requiredCapacity">requiredCapacity in the buffer</param>
        /// <returns>true if the buffer has the capacity to allocate the next sequence otherwise false.</returns>
        public override bool HasAvailableCapacity(int requiredCapacity)
        {
            long nextValue = _fields.NextValue;

            long wrapPoint = (nextValue + requiredCapacity) - _bufferSize;
            long cachedGatingSequence = _fields.CachedValue;

            if (wrapPoint > cachedGatingSequence || cachedGatingSequence > nextValue)
            {
                long minSequence = Util.GetMinimumSequence(_gatingSequences.ReadFullFence(), nextValue);
                _fields.CachedValue = minSequence;

                if (wrapPoint > minSequence)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Claim the next event in sequence for publishing.
        /// </summary>
        /// <returns></returns>
        public override long Next()
        {
            return Next(1);
        }

        /// <summary>
        /// Claim the next n events in sequence for publishing.  This is for batch event producing.  Using batch producing requires a little care and some math.
        /// <code>
        ///     int n = 10;
        ///     long hi = sequencer.next(n);
        ///     long lo = hi - (n - 1);
        ///     for (long sequence = lo; sequence &lt;= hi; sequence++) {
        ///        // Do work.
        ///     }
        ///     sequencer.publish(lo, hi);
        /// </code>
        /// </summary>
        /// <param name="n">the number of sequences to claim</param>
        /// <returns>the highest claimed sequence value</returns>
        public override long Next(int n)
        {
            if (n < 1)
            {
                throw new ArgumentException("n must be > 0");
            }

            long nextValue = _fields.NextValue;

            long nextSequence = nextValue + n;
            long wrapPoint = nextSequence - _bufferSize;
            long cachedGatingSequence = _fields.CachedValue;

            if (wrapPoint > cachedGatingSequence || cachedGatingSequence > nextValue)
            {
                var spinWait = default(SpinWait);
                long minSequence;
                while (wrapPoint > (minSequence = Util.GetMinimumSequence(_gatingSequences.ReadFullFence(), nextValue)))
                {
                    spinWait.SpinOnce(); // LockSupport.parkNanos(1L);
                }

                _fields.CachedValue = minSequence;
            }

            _fields.NextValue = nextSequence;

            return nextSequence;
        }

        /// <summary>
        /// Attempt to claim the next event in sequence for publishing.  Will return the number of the slot if there is at least<code>requiredCapacity</code> slots available.
        /// </summary>
        /// <returns>the claimed sequence value</returns>
        public override long TryNext()
        {
            return TryNext(1);
        }

        /// <summary>
        /// Attempt to claim the next event in sequence for publishing.  Will return the
        /// number of the slot if there is at least <param name="availableCapacity"></param> slots
        /// available. 
        /// </summary>
        /// <param name="n">the number of sequences to claim</param>
        /// <returns>the claimed sequence value</returns>
        public override long TryNext(int n)
        {
            if (n < 1)
            {
                throw new ArgumentException("n must be > 0");
            }

            if (!HasAvailableCapacity(n))
            {
                throw InsufficientCapacityException.Instance;
            }

            var newValue = _fields.NextValue + n;
            _fields.NextValue = newValue;
            return newValue;
        }

        /// <summary>
        /// Get the remaining capacity for this sequencer. return The number of slots remaining.
        /// </summary>
        public override long GetRemainingCapacity()
        {
            var nextValue = _fields.NextValue;
            long consumed = Util.GetMinimumSequence(_gatingSequences.ReadFullFence(), nextValue);
            long produced = nextValue;
            return BufferSize - (produced - consumed);
        }

        /// <summary>
        /// Claim a specific sequence when only one publisher is involved.
        /// </summary>
        /// <param name="sequence">sequence to be claimed.</param>
        /// <returns>sequence just claimed.</returns>
        public override long Claim(long sequence)
        {
            _fields.NextValue = sequence;
            return sequence;
        }

        /// <summary>
        /// Publish an event and make it visible to <see cref="IEventProcessor"/>s
        /// </summary>
        /// <param name="sequence">sequence to be published</param>
        public override void Publish(long sequence)
        {
            _cursor.Value = sequence;
            _waitStrategy.SignalAllWhenBlocking();
        }

        /// <summary>
        /// Batch publish sequences.  Called when all of the events have been filled.
        /// </summary>
        /// <param name="lo">first sequence number to publish</param>
        /// <param name="hi">last sequence number to publish</param>
        public override void Publish(long lo, long hi)
        {
            Publish(hi);
        }

        /// <summary>
        /// Confirms if a sequence is published and the event is available for use; non-blocking.
        /// </summary>
        /// <param name="sequence">sequence of the buffer to check</param>
        /// <returns>true if the sequence is available for use, false if not</returns>
        public override bool IsAvailable(long sequence) => sequence <= _cursor.Value;

        /// <summary>
        /// Get the highest sequence number that can be safely read from the ring buffer.  Depending
        /// on the implementation of the Sequencer this call may need to scan a number of values
        /// in the Sequencer.  The scan will range from nextSequence to availableSequence.  If
        /// there are no available values <code>&amp;gt;= nextSequence</code> the return value will be
        /// <code>nextSequence - 1</code>.  To work correctly a consumer should pass a value that
        /// it 1 higher than the last sequence that was successfully processed.
        /// </summary>
        /// <param name="nextSequence">The sequence to start scanning from.</param>
        /// <param name="availableSequence">The sequence to scan to.</param>
        /// <returns>The highest value that can be safely read, will be at least <code>nextSequence - 1</code>.</returns>
        public override long GetHighestPublishedSequence(long nextSequence, long availableSequence)
        {
            return availableSequence;
        }

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        private struct Fields
        {
            [FieldOffset(0)]
            private Padding56 _beforePadding;
            [FieldOffset(56)]
            public long NextValue;
            [FieldOffset(64)]
            public long CachedValue;
            [FieldOffset(72)]
            private Padding56 _afterPadding;

            public Fields(long nextValue, long cachedValue)
            {
                _beforePadding = default(Padding56);
                NextValue = nextValue;
                CachedValue = cachedValue;
                _afterPadding = default(Padding56);
            }
        }
    }
}