using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Disruptor
{
    [StructLayout(LayoutKind.Explicit, Size = 160)]
    public class SingleProducerSequencer : ISequencer
    {
        // padding: 56

        [FieldOffset(56)]
        private readonly IWaitStrategy _waitStrategy;

        [FieldOffset(64)]
        private readonly Sequence _cursor = new Sequence();

        [FieldOffset(72)]
        // volatile in the Java version => always use Volatile.Read/Write or Interlocked methods to access this field
        private ISequence[] _gatingSequences = new ISequence[0];

        [FieldOffset(80)]
        private readonly int _bufferSize;

        [FieldOffset(84)]
        private readonly bool _isBlockingWaitStrategy;

        // padding: 3

        [FieldOffset(88)]
        private long _nextValue = Sequence.InitialCursorValue;

        [FieldOffset(96)]
        private long _cachedValue = Sequence.InitialCursorValue;

        // padding: 56

        public SingleProducerSequencer(int bufferSize, IWaitStrategy waitStrategy)
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
        public bool HasAvailableCapacity(int requiredCapacity)
        {
            return HasAvailableCapacity(requiredCapacity, false);
        }

        private bool HasAvailableCapacity(int requiredCapacity, bool doStore)
        {
            long nextValue = _nextValue;

            long wrapPoint = (nextValue + requiredCapacity) - _bufferSize;
            long cachedGatingSequence = _cachedValue;

            if (wrapPoint > cachedGatingSequence || cachedGatingSequence > nextValue)
            {
                if (doStore)
                {
                    _cursor.SetValueVolatile(nextValue);
                }

                long minSequence = Util.GetMinimumSequence(Volatile.Read(ref _gatingSequences), nextValue);
                _cachedValue = minSequence;

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
                throw new ArgumentException("n must be > 0");
            }

            return NextInternal(n);
        }

        public long NextInternal(int n)
        {
            long nextValue = _nextValue;

            long nextSequence = nextValue + n;
            long wrapPoint = nextSequence - _bufferSize;
            long cachedGatingSequence = _cachedValue;

            if (wrapPoint > cachedGatingSequence || cachedGatingSequence > nextValue)
            {
                _cursor.SetValueVolatile(nextValue);

                var spinWait = default(AggressiveSpinWait);
                long minSequence;
                while (wrapPoint > (minSequence = Util.GetMinimumSequence(Volatile.Read(ref _gatingSequences), nextValue)))
                {
                    spinWait.SpinOnce();
                }

                _cachedValue = minSequence;
            }

            _nextValue = nextSequence;

            return nextSequence;
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
        public long TryNext()
        {
            return TryNextInternal(1);
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
        public long TryNext(int n)
        {
            if (n < 1)
            {
                throw new ArgumentException("n must be > 0");
            }

            return TryNextInternal(n);
        }

        internal long TryNextInternal(int n)
        {
            if (!HasAvailableCapacity(n, true))
            {
                throw InsufficientCapacityException.Instance;
            }

            var nextSequence = _nextValue + n;
            _nextValue = nextSequence;

            return nextSequence;
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
                throw new ArgumentException("n must be > 0");
            }

            return TryNextInternal(n, out sequence);
        }

        internal bool TryNextInternal(int n, out long sequence)
        {
            if (!HasAvailableCapacity(n, true))
            {
                sequence = default(long);
                return false;
            }

            var nextSequence = _nextValue + n;
            _nextValue = nextSequence;

            sequence = nextSequence;
            return true;
        }

        /// <summary>
        /// Get the remaining capacity for this sequencer. return The number of slots remaining.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetRemainingCapacity()
        {
            var nextValue = _nextValue;

            var consumed = Util.GetMinimumSequence(Volatile.Read(ref _gatingSequences), nextValue);
            var produced = nextValue;
            return BufferSize - (produced - consumed);
        }

        /// <summary>
        /// Claim a specific sequence when only one publisher is involved.
        /// </summary>
        /// <param name="sequence">sequence to be claimed.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Claim(long sequence)
        {
            _nextValue = sequence;
        }

        /// <summary>
        /// Publish an event and make it visible to <see cref="IEventProcessor"/>s
        /// </summary>
        /// <param name="sequence">sequence to be published</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish(long sequence)
        {
            _cursor.SetValue(sequence);

            if (_isBlockingWaitStrategy)
            {
                _waitStrategy.SignalAllWhenBlocking();
            }
        }

        /// <summary>
        /// Batch publish sequences.  Called when all of the events have been filled.
        /// </summary>
        /// <param name="lo">first sequence number to publish</param>
        /// <param name="hi">last sequence number to publish</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish(long lo, long hi)
        {
            Publish(hi);
        }

        /// <summary>
        /// Confirms if a sequence is published and the event is available for use; non-blocking.
        /// </summary>
        /// <param name="sequence">sequence of the buffer to check</param>
        /// <returns>true if the sequence is available for use, false if not</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAvailable(long sequence)
        {
            return sequence <= _cursor.Value;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetHighestPublishedSequence(long nextSequence, long availableSequence)
        {
            return availableSequence;
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
