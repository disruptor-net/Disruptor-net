using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor.Internal;

namespace Disruptor
{
    /// <summary>
    /// Base type for unmanaged-memory-backed ring buffers.
    ///
    /// <see cref="UnmanagedRingBuffer{T}"/>.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 148)]
    public abstract class UnmanagedRingBuffer : ICursored
    {
        // padding: 56

        [FieldOffset(56)]
        protected IntPtr _entries;

        [FieldOffset(64)]
        protected long _indexMask;

        [FieldOffset(72)]
        protected int _eventSize;

        [FieldOffset(76)]
        protected int _bufferSize;

        [FieldOffset(80)]
        protected SequencerDispatcher _sequencerDispatcher; // includes 7 bytes of padding

        // padding: 52

        /// <summary>
        /// Construct a UnmanagedRingBuffer with the full option set.
        /// </summary>
        /// <param name="sequencer">sequencer to handle the ordering of events moving through the UnmanagedRingBuffer.</param>
        /// <param name="pointer">pointer to the first element of the buffer</param>
        /// <param name="eventSize">size of each event</param>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        protected UnmanagedRingBuffer(ISequencer sequencer, IntPtr pointer, int eventSize)
        {
            if (eventSize < 1)
            {
                throw new ArgumentException("eventSize must not be less than 1");
            }
            if (sequencer.BufferSize < 1)
            {
                throw new ArgumentException("bufferSize must not be less than 1");
            }
            if (!sequencer.BufferSize.IsPowerOf2())
            {
                throw new ArgumentException("bufferSize must be a power of 2");
            }

            _sequencerDispatcher = new SequencerDispatcher(sequencer);
            _bufferSize = sequencer.BufferSize;
            _entries = pointer;
            _indexMask = _bufferSize - 1;
            _eventSize = eventSize;
        }

        /// <summary>
        /// Gets the size of the buffer.
        /// </summary>
        public int BufferSize => _bufferSize;

        /// <summary>
        /// Given specified <paramref name="requiredCapacity"/> determines if that amount of space
        /// is available.  Note, you can not assume that if this method returns <c>true</c>
        /// that a call to <see cref="Next()"/> will not block.  Especially true if this
        /// ring buffer is set up to handle multiple producers.
        /// </summary>
        /// <param name="requiredCapacity">The capacity to check for.</param>
        /// <returns><c>true</c> if the specified <paramref name="requiredCapacity"/> is available <c>false</c> if not.</returns>
        public bool HasAvailableCapacity(int requiredCapacity)
        {
            return _sequencerDispatcher.Sequencer.HasAvailableCapacity(requiredCapacity);
        }

        /// <summary>
        /// Increment and return the next sequence for the ring buffer.  Calls of this
        /// method should ensure that they always publish the sequence afterward. E.g.
        /// <code>
        /// long sequence = ringBuffer.Next();
        /// try
        /// {
        ///     Event e = ringBuffer[sequence];
        ///     // Do some work with the event.
        /// }
        /// finally
        /// {
        ///     ringBuffer.Publish(sequence);
        /// }
        /// </code>
        /// </summary>
        /// <returns>The next sequence to publish to.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Next()
        {
            return _sequencerDispatcher.Next();
        }

        /// <summary>
        /// The same functionality as <see cref="Next()"/>, but allows the caller to claim
        /// the next n sequences.
        /// </summary>
        /// <param name="n">number of slots to claim</param>
        /// <returns>sequence number of the highest slot claimed</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Next(int n)
        {
            if (n < 1 || n > _bufferSize)
            {
                ThrowHelper.ThrowArgMustBeGreaterThanZeroAndLessThanBufferSize();
            }

            return _sequencerDispatcher.Next(n);
        }

        /// <summary>
        /// Increment and return the next sequence for the ring buffer.  Calls of this
        /// method should ensure that they always publish the sequence afterward. E.g.
        /// <code>
        /// if (!ringBuffer.TryNext(out var sequence))
        /// {
        ///     // Handle full ring buffer
        ///     return;
        /// }
        /// try
        /// {
        ///     Event e = ringBuffer[sequence];
        ///     // Do some work with the event.
        /// }
        /// finally
        /// {
        ///     ringBuffer.Publish(sequence);
        /// }
        /// </code>
        /// This method will not block if there is not space available in the ring
        /// buffer, instead it will return false.
        /// </summary>
        /// <param name="sequence">the next sequence to publish to</param>
        /// <returns>true if the necessary space in the ring buffer is not available, otherwise false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryNext(out long sequence)
        {
            return _sequencerDispatcher.TryNext(out sequence);
        }

        /// <summary>
        /// The same functionality as <see cref="TryNext(out long)"/>, but allows the caller to attempt
        /// to claim the next n sequences.
        /// </summary>
        /// <param name="n">number of slots to claim</param>
        /// <param name="sequence">sequence number of the highest slot claimed</param>
        /// <returns>true if the necessary space in the ring buffer is not available, otherwise false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryNext(int n, out long sequence)
        {
            if (n < 1 || n > _bufferSize)
            {
                ThrowHelper.ThrowArgMustBeGreaterThanZeroAndLessThanBufferSize();
            }

            return _sequencerDispatcher.TryNext(n, out sequence);
        }

        /// <summary>
        /// Resets the cursor to a specific value.  This can be applied at any time, but it is worth noting
        /// that it can cause a data race and should only be used in controlled circumstances.  E.g. during
        /// initialisation.
        /// </summary>
        /// <param name="sequence">the sequence to reset too.</param>
        [Obsolete]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResetTo(long sequence)
        {
            _sequencerDispatcher.Sequencer.Claim(sequence);
            _sequencerDispatcher.Sequencer.Publish(sequence);
        }

        /// <summary>
        /// Add the specified gating sequences to this instance of the Disruptor.  They will
        /// safely and atomically added to the list of gating sequences.
        /// </summary>
        /// <param name="gatingSequences">the sequences to add.</param>
        public void AddGatingSequences(params ISequence[] gatingSequences)
        {
            _sequencerDispatcher.Sequencer.AddGatingSequences(gatingSequences);
        }

        /// <summary>
        /// Get the minimum sequence value from all of the gating sequences
        /// added to this ringBuffer.
        /// </summary>
        /// <returns>the minimum gating sequence or the cursor sequence if no sequences have been added.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetMinimumGatingSequence()
        {
            return _sequencerDispatcher.Sequencer.GetMinimumSequence();
        }

        /// <summary>
        /// Remove the specified sequence from this ringBuffer.
        /// </summary>
        /// <param name="sequence">sequence to be removed.</param>
        /// <returns><c>true</c> if this sequence was found, <c>false</c> otherwise.</returns>
        public bool RemoveGatingSequence(ISequence sequence)
        {
            return _sequencerDispatcher.Sequencer.RemoveGatingSequence(sequence);
        }

        /// <summary>
        /// Create a new SequenceBarrier to be used by an EventProcessor to track which messages
        /// are available to be read from the ring buffer given a list of sequences to track.
        /// </summary>
        /// <param name="sequencesToTrack">the additional sequences to track</param>
        /// <returns>A sequence barrier that will track the specified sequences.</returns>
        public ISequenceBarrier NewBarrier(params ISequence[] sequencesToTrack)
        {
            return _sequencerDispatcher.Sequencer.NewBarrier(sequencesToTrack);
        }

        /// <summary>
        /// Get the current cursor value for the ring buffer.  The actual value received
        /// will depend on the type of <see cref="ISequencer"/> that is being used.
        /// </summary>
        public long Cursor => _sequencerDispatcher.Sequencer.Cursor;

        /// <summary>
        /// Publish the specified sequence.  This action marks this particular
        /// message as being available to be read.
        /// </summary>
        /// <param name="sequence">the sequence to publish.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish(long sequence)
        {
            _sequencerDispatcher.Publish(sequence);
        }

        /// <summary>
        /// Publish the specified sequences.  This action marks these particular
        /// messages as being available to be read.
        /// </summary>
        /// <param name="lo">the lowest sequence number to be published</param>
        /// <param name="hi">the highest sequence number to be published</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish(long lo, long hi)
        {
            _sequencerDispatcher.Publish(lo, hi);
        }

        /// <summary>
        /// Get the remaining capacity for this ringBuffer.
        /// </summary>
        /// <returns>The number of slots remaining.</returns>
        public long GetRemainingCapacity()
        {
            return _sequencerDispatcher.Sequencer.GetRemainingCapacity();
        }

        public override string ToString()
        {
            return $"UnmanagedRingBuffer{{bufferSize={_bufferSize}sequencer={_sequencerDispatcher.Sequencer}}}";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected void ThrowInvalidPublishCountException()
        {
            throw new ArgumentException($"Invalid publish count: It should be >= 0 and <= {_bufferSize}");
        }
    }
}
