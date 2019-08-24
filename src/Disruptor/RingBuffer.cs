using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Disruptor
{
    /// <summary>
    /// Ring based store of reusable entries containing the data representing
    /// an event being exchanged between event producer and <see cref="IEventProcessor"/>s.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 144)]
    public abstract class RingBuffer : ICursored
    {
        protected static readonly int _bufferPad = 128 / IntPtr.Size;

        // padding: 56

        [FieldOffset(56)]
        protected object _entries;

        [FieldOffset(64)]
        protected long _indexMask;

        [FieldOffset(72)]
        protected int _bufferSize;

        [FieldOffset(76)]
        protected RingBufferSequencerType _sequencerType;

        // padding: 3

        [FieldOffset(80)]
        protected SingleProducerSequencer _singleProducerSequencer;

        [FieldOffset(80)]
        protected MultiProducerSequencer _multiProducerSequencer;

        [FieldOffset(80)]
        protected ISequencer _sequencer;

        // padding: 56

        protected enum RingBufferSequencerType : byte
        {
            SingleProducer,
            MultiProducer,
            Unknown,
        }

        /// <summary>
        /// Construct a RingBuffer with the full option set.
        /// </summary>
        /// <param name="sequencer">sequencer to handle the ordering of events moving through the RingBuffer.</param>
        /// <param name="elementType">type of the RingBuffer elements</param>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        protected RingBuffer(ISequencer sequencer, Type elementType)
        {
            _sequencer = sequencer;
            _sequencerType = GetSequencerType();
            _bufferSize = sequencer.BufferSize;

            if (_bufferSize < 1)
            {
                throw new ArgumentException("bufferSize must not be less than 1");
            }
            if (!_bufferSize.IsPowerOf2())
            {
                throw new ArgumentException("bufferSize must be a power of 2");
            }

            _indexMask = _bufferSize - 1;
            _entries = Array.CreateInstance(elementType, _bufferSize + 2 * _bufferPad);

            RingBufferSequencerType GetSequencerType()
            {
                switch (sequencer)
                {
                    case SingleProducerSequencer s:
                        return RingBufferSequencerType.SingleProducer;
                    case MultiProducerSequencer m:
                        return RingBufferSequencerType.MultiProducer;
                    default:
                        return RingBufferSequencerType.Unknown;
                }
            }
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
            return _sequencer.HasAvailableCapacity(requiredCapacity);
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
            switch (_sequencerType)
            {
                case RingBufferSequencerType.SingleProducer:
                    return _singleProducerSequencer.NextInternal(1);
                case RingBufferSequencerType.MultiProducer:
                    return _multiProducerSequencer.NextInternal(1);
                default:
                    return _sequencer.Next();
            }
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

            switch (_sequencerType)
            {
                case RingBufferSequencerType.SingleProducer:
                    return _singleProducerSequencer.NextInternal(n);
                case RingBufferSequencerType.MultiProducer:
                    return _multiProducerSequencer.NextInternal(n);
                default:
                    return _sequencer.Next(n);
            }
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
            switch (_sequencerType)
            {
                case RingBufferSequencerType.SingleProducer:
                    return _singleProducerSequencer.TryNextInternal(1, out sequence);
                case RingBufferSequencerType.MultiProducer:
                    return _multiProducerSequencer.TryNextInternal(1, out sequence);
                default:
                    return _sequencer.TryNext(out sequence);
            }
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

            switch (_sequencerType)
            {
                case RingBufferSequencerType.SingleProducer:
                    return _singleProducerSequencer.TryNextInternal(n, out sequence);
                case RingBufferSequencerType.MultiProducer:
                    return _multiProducerSequencer.TryNextInternal(n, out sequence);
                default:
                    return _sequencer.TryNext(n, out sequence);
            }
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
            _sequencer.Claim(sequence);
            _sequencer.Publish(sequence);
        }

        /// <summary>
        /// Add the specified gating sequences to this instance of the Disruptor.  They will
        /// safely and atomically added to the list of gating sequences.
        /// </summary>
        /// <param name="gatingSequences">the sequences to add.</param>
        public void AddGatingSequences(params ISequence[] gatingSequences)
        {
            _sequencer.AddGatingSequences(gatingSequences);
        }

        /// <summary>
        /// Get the minimum sequence value from all of the gating sequences
        /// added to this ringBuffer.
        /// </summary>
        /// <returns>the minimum gating sequence or the cursor sequence if no sequences have been added.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetMinimumGatingSequence()
        {
            return _sequencer.GetMinimumSequence();
        }

        /// <summary>
        /// Remove the specified sequence from this ringBuffer.
        /// </summary>
        /// <param name="sequence">sequence to be removed.</param>
        /// <returns><c>true</c> if this sequence was found, <c>false</c> otherwise.</returns>
        public bool RemoveGatingSequence(ISequence sequence)
        {
            return _sequencer.RemoveGatingSequence(sequence);
        }

        /// <summary>
        /// Create a new SequenceBarrier to be used by an EventProcessor to track which messages
        /// are available to be read from the ring buffer given a list of sequences to track.
        /// </summary>
        /// <param name="sequencesToTrack">the additional sequences to track</param>
        /// <returns>A sequence barrier that will track the specified sequences.</returns>
        public ISequenceBarrier NewBarrier(params ISequence[] sequencesToTrack)
        {
            return _sequencer.NewBarrier(sequencesToTrack);
        }

        /// <summary>
        /// Get the current cursor value for the ring buffer.  The actual value received
        /// will depend on the type of <see cref="ISequencer"/> that is being used.
        /// </summary>
        public long Cursor => _sequencer.Cursor;

        /// <summary>
        /// Publish the specified sequence.  This action marks this particular
        /// message as being available to be read.
        /// </summary>
        /// <param name="sequence">the sequence to publish.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish(long sequence)
        {
            switch (_sequencerType)
            {
                case RingBufferSequencerType.SingleProducer:
                    _singleProducerSequencer.Publish(sequence);
                    break;
                case RingBufferSequencerType.MultiProducer:
                    _multiProducerSequencer.Publish(sequence);
                    break;
                default:
                    _sequencer.Publish(sequence);
                    break;
            }
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
            switch (_sequencerType)
            {
                case RingBufferSequencerType.SingleProducer:
                    _singleProducerSequencer.Publish(hi);
                    break;
                case RingBufferSequencerType.MultiProducer:
                    _multiProducerSequencer.Publish(lo, hi);
                    break;
                default:
                    _sequencer.Publish(lo, hi);
                    break;
            }
        }

        /// <summary>
        /// Get the remaining capacity for this ringBuffer.
        /// </summary>
        /// <returns>The number of slots remaining.</returns>
        public long GetRemainingCapacity()
        {
            return _sequencer.GetRemainingCapacity();
        }

        public override string ToString()
        {
            return "RingBuffer{" +
                   "bufferSize=" + _bufferSize +
                   "sequencer=" + _sequencer +
                   "}";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected void ThrowInvalidPublishCountException()
        {
            throw new ArgumentException($"Invalid publish count: It should be >= 0 and <= {_bufferSize}");
        }
    }
}
