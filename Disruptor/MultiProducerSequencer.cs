using System;
using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// <para>Coordinator for claiming sequences for access to a data structure while tracking dependent <see cref="Sequence"/>s.
    /// Suitable for use for sequencing across multiple publisher threads.</para>
    /// <para/>
    /// <para/>Note on <see cref="Sequencer.Cursor"/>:  With this sequencer the cursor value is updated after the call
    /// to <see cref="Sequencer.Next()"/>, to determine the highest available sequence that can be read, then
    /// <see cref="GetHighestPublishedSequence"/> should be used. 
    /// </summary>
    public class MultiProducerSequencer : Sequencer
    {
        private readonly Sequence _gatingSequenceCache = new Sequence();

        // availableBuffer tracks the state of each ringbuffer slot
        // see below for more details on the approach
        private readonly int[] _availableBuffer;
        private readonly int _indexMask;
        private readonly int _indexShift;

        public MultiProducerSequencer(int bufferSize, IWaitStrategy waitStrategy)
            : base(bufferSize, waitStrategy)
        {
            _availableBuffer = new int[bufferSize];
            _indexMask = bufferSize - 1;
            _indexShift = Util.Log2(bufferSize);
            InitialiseAvailableBuffer();
        }

        /// <summary>
        /// Has the buffer got capacity to allocate another sequence.  This is a concurrent
        /// method so the response should only be taken as an indication of available capacity.
        /// </summary>
        /// <param name="requiredCapacity">requiredCapacity in the buffer</param>
        /// <returns>true if the buffer has the capacity to allocate the next sequence otherwise false.</returns>
        public override bool HasAvailableCapacity(int requiredCapacity)
        {
            return HasAvailableCapacity(Volatile.Read(ref _gatingSequences), requiredCapacity, _cursor.Value);
        }

        private bool HasAvailableCapacity(ISequence[] gatingSequences, int requiredCapacity, long cursorValue)
        {
            var wrapPoint = (cursorValue + requiredCapacity) - _bufferSize;
            var cachedGatingSequence = _gatingSequenceCache.Value;

            if (wrapPoint > cachedGatingSequence || cachedGatingSequence > cursorValue)
            {
                long minSequence = Util.GetMinimumSequence(gatingSequences, cursorValue);
                _gatingSequenceCache.SetValue(minSequence);

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

            long current;
            long next;

            var spinWait = default(SpinWait);
            do
            {
                current = _cursor.Value;
                next = current + n;

                long wrapPoint = next - _bufferSize;
                long cachedGatingSequence = _gatingSequenceCache.Value;

                if (wrapPoint > cachedGatingSequence || cachedGatingSequence > current)
                {
                    long gatingSequence = Util.GetMinimumSequence(Volatile.Read(ref _gatingSequences), current);

                    if (wrapPoint > gatingSequence)
                    {
                        spinWait.SpinOnce(); // LockSupport.parkNanos(1L);
                        continue;
                    }

                    _gatingSequenceCache.SetValue(gatingSequence);
                }
                else if (_cursor.CompareAndSet(current, next))
                {
                    break;
                }
            }
            while (true);

            return next;
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
        /// number of the slot if there is at least <param name="n"></param> slots
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

            long current;
            long next;

            do
            {
                current = _cursor.Value;
                next = current + n;

                if (!HasAvailableCapacity(Volatile.Read(ref _gatingSequences), n, current))
                {
                    throw InsufficientCapacityException.Instance;
                }
            }
            while (!_cursor.CompareAndSet(current, next));

            return next;
        }

        /// <summary>
        /// Get the remaining capacity for this sequencer. return The number of slots remaining.
        /// </summary>
        public override long GetRemainingCapacity()
        {
            var consumed = Util.GetMinimumSequence(Volatile.Read(ref _gatingSequences));
            var produced = _cursor.Value;
            return BufferSize - (produced - consumed);
        }

        private void InitialiseAvailableBuffer()
        {
            for (int i = _availableBuffer.Length - 1; i != 0; i--)
            {
                SetAvailableBufferValue(i, -1);
            }

            SetAvailableBufferValue(0, -1);
        }

        /// <summary>
        /// Publish an event and make it visible to <see cref="IEventProcessor"/>s
        /// </summary>
        /// <param name="sequence">sequence to be published</param>
        public override void Publish(long sequence)
        {
            SetAvailable(sequence);
            _waitStrategy.SignalAllWhenBlocking();
        }

        /// <summary>
        /// Publish an event and make it visible to <see cref="IEventProcessor"/>s
        /// </summary>
        public override void Publish(long lo, long hi)
        {
            for (long l = lo; l <= hi; l++)
            {
                SetAvailable(l);
            }
            _waitStrategy.SignalAllWhenBlocking();
        }

        private void SetAvailable(long sequence)
        {
            SetAvailableBufferValue(CalculateIndex(sequence), CalculateAvailabilityFlag(sequence));
        }

        private unsafe void SetAvailableBufferValue(int index, int flag)
        {
            fixed (int* buffer = _availableBuffer)
            {
                buffer[index] = flag;
            }
        }

        /// <summary>
        /// Claim a specific sequence when only one publisher is involved.
        /// </summary>
        /// <param name="sequence">sequence to be claimed.</param>
        /// <returns>sequence just claimed.</returns>
        public override long Claim(long sequence)
        {
            _cursor.SetValue(sequence);
            return sequence;
        }

        /// <summary>
        /// Confirms if a sequence is published and the event is available for use; non-blocking.
        /// </summary>
        /// <param name="sequence">sequence of the buffer to check</param>
        /// <returns>true if the sequence is available for use, false if not</returns>
        public override unsafe bool IsAvailable(long sequence)
        {
            int index = CalculateIndex(sequence);
            int flag = CalculateAvailabilityFlag(sequence);
            fixed (int* buffer = _availableBuffer)
            {
                return Volatile.Read(ref buffer[index]) == flag;
            }
        }

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
        public override long GetHighestPublishedSequence(long lowerBound, long availableSequence)
        {
            for (long sequence = lowerBound; sequence <= availableSequence; sequence++)
            {
                if (!IsAvailable(sequence))
                {
                    return sequence - 1;
                }
            }

            return availableSequence;
        }

        private int CalculateAvailabilityFlag(long sequence)
        {
            return (int)((ulong)sequence >> _indexShift);
        }

        private int CalculateIndex(long sequence)
        {
            return ((int)sequence) & _indexMask;
        }
    }
}