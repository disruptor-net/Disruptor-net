using System;
using System.Runtime.CompilerServices;
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
    public class MultiProducerSequencer : ISequencer
    {
        private readonly int _bufferSize;
        private readonly IWaitStrategy _waitStrategy;
        private readonly bool _isBlockingWaitStrategy;
        private readonly Sequence _cursor = new Sequence();

        // volatile in the Java version => always use Volatile.Read/Write or Interlocked methods to access this field
        private ISequence[] _gatingSequences = new ISequence[0];

        private readonly Sequence _gatingSequenceCache = new Sequence();

        // availableBuffer tracks the state of each ringbuffer slot
        // see below for more details on the approach
        private readonly int[] _availableBuffer;
        private readonly int _indexMask;
        private readonly int _indexShift;

        public MultiProducerSequencer(int bufferSize, IWaitStrategy waitStrategy)
        {
            if (bufferSize < 1)
            {
                throw new ArgumentException("bufferSize must not be less than 1");
            }
            if (!bufferSize.IsPowerOf2())
            {
                throw new ArgumentException("bufferSize must be a power of 2");
            }

            _bufferSize = bufferSize;
            _waitStrategy = waitStrategy;
            _isBlockingWaitStrategy = !(waitStrategy is INonBlockingWaitStrategy);
            _availableBuffer = new int[bufferSize];
            _indexMask = bufferSize - 1;
            _indexShift = Util.Log2(bufferSize);

            InitialiseAvailableBuffer();
        }

        /// <summary>
        /// Create a <see cref="ISequenceBarrier"/> that gates on the the cursor and a list of <see cref="Sequence"/>s
        /// </summary>
        /// <param name="sequencesToTrack"></param>
        /// <returns></returns>
        public ISequenceBarrier NewBarrier(params ISequence[] sequencesToTrack)
        {
            return ProcessingSequenceBarrierFactory.Create(this, _waitStrategy, _cursor, sequencesToTrack);
        }

        /// <summary>
        /// The capacity of the data structure to hold entries.
        /// </summary>
        public int BufferSize => _bufferSize;

        /// <summary>
        /// Get the value of the cursor indicating the published sequence.
        /// </summary>
        public long Cursor => _cursor.Value;

        /// <summary>
        /// Has the buffer got capacity to allocate another sequence.  This is a concurrent
        /// method so the response should only be taken as an indication of available capacity.
        /// </summary>
        /// <param name="requiredCapacity">requiredCapacity in the buffer</param>
        /// <returns>true if the buffer has the capacity to allocate the next sequence otherwise false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasAvailableCapacity(int requiredCapacity)
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
        /// Claim a specific sequence when only one publisher is involved.
        /// </summary>
        /// <param name="sequence">sequence to be claimed.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Claim(long sequence)
        {
            _cursor.SetValue(sequence);
        }

        /// <summary>
        /// Claim the next event in sequence for publishing.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Next()
        {
            return NextInternal(1);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Next(int n)
        {
            if (n < 1)
            {
                ThrowHelper.ThrowArgMustBeGreaterThanZero();
            }

            return NextInternal(n);
        }

        internal long NextInternal(int n)
        {
            long current;
            long next;

            var spinWait = default(AggressiveSpinWait);
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
                        spinWait.SpinOnce();
                        continue;
                    }

                    _gatingSequenceCache.SetValue(gatingSequence);
                }
                else if (_cursor.CompareAndSet(current, next))
                {
                    break;
                }
            } while (true);

            return next;
        }

        /// <summary>
        /// Attempt to claim the next event for publishing.  Will return the
        /// number of the slot if there is at least one slot available.
        /// 
        /// Have a look at <see cref="Next()"/> for a description on how to
        /// use this method.
        /// </summary>
        /// <returns>the claimed sequence value</returns>
        /// <exception cref="InsufficientCapacityException">there is no space available in the ring buffer.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use TryNext(out long) instead.")]
        public long TryNext()
        {
            return TryNext(1);
        }

        /// <summary>
        /// Attempt to claim the next <code>n</code> events in sequence for publishing.
        /// Will return the highest numbered slot if there is at least <code>n</code> slots
        /// available.
        /// 
        /// Have a look at <see cref="Next(int)"/> for a description on how to
        /// use this method.
        /// </summary>
        /// <param name="n">the number of sequences to claim</param>
        /// <returns>the claimed sequence value</returns>
        /// <exception cref="InsufficientCapacityException">there is no space available in the ring buffer.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use TryNext(int, out long) instead.")]
        public long TryNext(int n)
        {
            if (n < 1)
            {
                ThrowHelper.ThrowArgMustBeGreaterThanZero();
            }

            return TryNextInternal(n);
        }

        internal long TryNextInternal(int n)
        {
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
            } while (!_cursor.CompareAndSet(current, next));

            return next;
        }

        /// <summary>
        /// Attempt to claim the next event for publishing.  Will return the
        /// number of the slot if there is at least one slot available.
        /// 
        /// Have a look at <see cref="Next()"/> for a description on how to
        /// use this method.
        /// </summary>
        /// <param name="sequence">the claimed sequence value</param>
        /// <returns>true of there is space available in the ring buffer, otherwise false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryNext(out long sequence)
        {
            return TryNextInternal(1, out sequence);
        }

        /// <summary>
        /// Attempt to claim the next <code>n</code> events in sequence for publishing.
        /// Will return the highest numbered slot if there is at least <code>n</code> slots
        /// available.
        /// 
        /// Have a look at <see cref="Next(int)"/> for a description on how to
        /// use this method.
        /// </summary>
        /// <param name="n">the number of sequences to claim</param>
        /// <param name="sequence">the claimed sequence value</param>
        /// <returns>true of there is space available in the ring buffer, otherwise false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryNext(int n, out long sequence)
        {
            if (n < 1)
            {
                ThrowHelper.ThrowArgMustBeGreaterThanZero();
            }

            return TryNextInternal(n, out sequence);
        }

        internal bool TryNextInternal(int n, out long sequence)
        {
            long current;
            long next;

            do
            {
                current = _cursor.Value;
                next = current + n;

                if (!HasAvailableCapacity(Volatile.Read(ref _gatingSequences), n, current))
                {
                    sequence = default(long);
                    return false;
                }
            } while (!_cursor.CompareAndSet(current, next));

            sequence = next;
            return true;
        }

        /// <summary>
        /// Get the remaining capacity for this sequencer. return The number of slots remaining.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetRemainingCapacity()
        {
            var consumed = Util.GetMinimumSequence(Volatile.Read(ref _gatingSequences), _cursor.Value);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish(long sequence)
        {
            SetAvailableBufferValue(CalculateIndex(sequence), CalculateAvailabilityFlag(sequence));

            if (_isBlockingWaitStrategy)
            {
                _waitStrategy.SignalAllWhenBlocking();
            }
        }

        /// <summary>
        /// Publish an event and make it visible to <see cref="IEventProcessor"/>s
        /// </summary>
        public void Publish(long lo, long hi)
        {
            for (long l = lo; l <= hi; l++)
            {
                SetAvailableBufferValue(CalculateIndex(l), CalculateAvailabilityFlag(l));
            }

            if (_isBlockingWaitStrategy)
            {
                _waitStrategy.SignalAllWhenBlocking();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetAvailableBufferValue(int index, int flag)
        {
            _availableBuffer[index] = flag;
        }

        /// <summary>
        /// Confirms if a sequence is published and the event is available for use; non-blocking.
        /// </summary>
        /// <param name="sequence">sequence of the buffer to check</param>
        /// <returns>true if the sequence is available for use, false if not</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAvailable(long sequence)
        {
            int index = CalculateIndex(sequence);
            int flag = CalculateAvailabilityFlag(sequence);

            return Volatile.Read(ref _availableBuffer[index]) == flag;
        }

        /// <summary>
        /// Get the highest sequence number that can be safely read from the ring buffer.  Depending
        /// on the implementation of the Sequencer this call may need to scan a number of values
        /// in the Sequencer.  The scan will range from nextSequence to availableSequence.  If
        /// there are no available values <code>&amp;gt;= nextSequence</code> the return value will be
        /// <code>nextSequence - 1</code>.  To work correctly a consumer should pass a value that
        /// it 1 higher than the last sequence that was successfully processed.
        /// </summary>
        /// <param name="lowerBound">The sequence to start scanning from.</param>
        /// <param name="availableSequence">The sequence to scan to.</param>
        /// <returns>The highest value that can be safely read, will be at least <code>nextSequence - 1</code>.</returns>
        public long GetHighestPublishedSequence(long lowerBound, long availableSequence)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int CalculateAvailabilityFlag(long sequence)
        {
            return (int)((ulong)sequence >> _indexShift);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int CalculateIndex(long sequence)
        {
            return ((int)sequence) & _indexMask;
        }

        /// <summary>
        /// Add the specified gating sequences to this instance of the Disruptor.  They will
        /// safely and atomically added to the list of gating sequences. 
        /// </summary>
        /// <param name="gatingSequences">The sequences to add.</param>
        public void AddGatingSequences(params ISequence[] gatingSequences)
        {
            SequenceGroups.AddSequences(ref _gatingSequences, this, gatingSequences);
        }

        /// <summary>
        /// Remove the specified sequence from this sequencer.
        /// </summary>
        /// <param name="sequence">to be removed.</param>
        /// <returns>true if this sequence was found, false otherwise.</returns>
        public bool RemoveGatingSequence(ISequence sequence)
        {
            return SequenceGroups.RemoveSequence(ref _gatingSequences, sequence);
        }

        /// <summary>
        /// Get the minimum sequence value from all of the gating sequences
        /// added to this ringBuffer.
        /// </summary>
        /// <returns>The minimum gating sequence or the cursor sequence if no sequences have been added.</returns>
        public long GetMinimumSequence()
        {
            return Util.GetMinimumSequence(Volatile.Read(ref _gatingSequences), _cursor.Value);
        }

        /// <summary>
        /// Creates an event poller for this sequence that will use the supplied data provider and
        /// gating sequences.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="provider">The data source for users of this event poller</param>
        /// <param name="gatingSequences">Sequence to be gated on.</param>
        /// <returns>A poller that will gate on this ring buffer and the supplied sequences.</returns>
        public EventPoller<T> NewPoller<T>(IDataProvider<T> provider, params ISequence[] gatingSequences)
        {
            return EventPoller<T>.NewInstance(provider, this, new Sequence(), _cursor, gatingSequences);
        }
    }
}
