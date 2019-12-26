using System;
using System.Runtime.CompilerServices;
using Disruptor.Dsl;

namespace Disruptor
{
    /// <summary>
    /// Ring based store of reusable entries containing the data representing
    /// an event being exchanged between event producer and <see cref="IEventProcessor"/>s.
    ///
    /// The underlying storage is an unmanaged buffer. The buffer must be preallocated.
    /// </summary>
    /// <typeparam name="T">implementation storing the data for sharing during exchange or parallel coordination of an event.</typeparam>
    public sealed class UnmanagedRingBuffer<T> : UnmanagedRingBuffer, IValueRingBuffer<T>
        where T : unmanaged
    {
        /// <summary>
        /// Construct an UnmanagedRingBuffer with the full option set.
        /// </summary>
        /// <param name="pointer">pointer to the first element of the buffer</param>
        /// <param name="eventSize">size of each event</param>
        /// <param name="sequencer">sequencer to handle the ordering of events moving through the ring buffer.</param>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public UnmanagedRingBuffer(IntPtr pointer, int eventSize, ISequencer sequencer)
            : base(sequencer, pointer, eventSize)
        {
        }

        /// <summary>
        /// Construct an UnmanagedRingBuffer with the full option set.
        /// The <see cref="UnmanagedRingBufferMemory"/> is not owned by the ring buffer and should be disposed after shutdown.
        /// </summary>
        /// <param name="memory">block of memory that will store the events</param>
        /// <param name="producerType">producer type to use <see cref="ProducerType" /></param>
        /// <param name="waitStrategy">used to determine how to wait for new elements to become available.</param>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public UnmanagedRingBuffer(UnmanagedRingBufferMemory memory, ProducerType producerType, IWaitStrategy waitStrategy)
            : base(Sequencer.Create(producerType, memory.EventCount, waitStrategy), memory.PointerToFirstEvent, memory.EventSize)
        {
        }

        /// <summary>
        /// Get the event for a given sequence in the RingBuffer.
        ///
        /// This call has 2 uses.  Firstly use this call when publishing to a ring buffer.
        /// After calling <see cref="RingBuffer.Next()"/> use this call to get hold of the
        /// preallocated event to fill with data before calling <see cref="RingBuffer.Publish(long)"/>.
        ///
        /// Secondly use this call when consuming data from the ring buffer.  After calling
        /// <see cref="ISequenceBarrier.WaitFor"/> call this method with any value greater than
        /// that your current consumer sequence and less than or equal to the value returned from
        /// the <see cref="ISequenceBarrier.WaitFor"/> method.
        /// </summary>
        /// <param name="sequence">sequence for the event</param>
        /// <returns>the event for the given sequence</returns>
        public ref T this[long sequence]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return ref Util.ReadValue<T>(_entries, (int)(sequence & _indexMask), _eventSize);
            }
        }

         /// <summary>
        /// Increment the ring buffer sequence and return a scope that will publish the sequence on disposing.
        /// This method will block until there is space available in the ring buffer.
        /// buffer
        /// <code>
        /// using (var scope = _ringBuffer.PublishEvent())
        /// {
        ///     ref var e = ref scope.Event();
        ///     // Do some work with the event.
        /// }
        /// </code>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnpublishedEventScope PublishEvent()
        {
            var sequence = Next();
            return new UnpublishedEventScope(this, sequence);
        }

        /// <summary>
        /// Try to increment the ring buffer sequence and return a scope that will publish the sequence on disposing.
        /// This method will not block if there is not space available in the ring buffer.
        /// <code>
        /// using (var scope = _ringBuffer.TryPublishEvent())
        /// {
        ///     if (!scope.TryGetEvent(out var eventRef))
        ///         return;
        ///
        ///     ref var e = ref eventRef.Event();
        ///     // Do some work with the event.
        /// }
        /// </code>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NullableUnpublishedEventScope TryPublishEvent()
        {
            var success = TryNext(out var sequence);
            return new NullableUnpublishedEventScope(success ? this : null, sequence);
        }

        /// <summary>
        /// Increment the ring buffer sequence by <paramref name="count"/> and return a scope that will publish the sequences on disposing.
        /// This method will block until there is space available in the ring buffer.
        /// <code>
        /// using (var scope = _ringBuffer.PublishEvents(2))
        /// {
        ///     ref var e1 = ref scope.Event(0);
        ///     ref var e2 = ref scope.Event(1);
        ///     // Do some work with the events.
        /// }
        /// </code>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnpublishedEventBatchScope PublishEvents(int count)
        {
            if ((uint)count > _bufferSize)
                ThrowInvalidPublishCountException();

            var endSequence = Next(count);
            return new UnpublishedEventBatchScope(this, endSequence + 1 - count, endSequence);
        }

        /// <summary>
        /// Try to increment the ring buffer sequence by <paramref name="count"/> and return a scope that will publish the sequences on disposing.
        /// This method will not block if there is not space available in the ring buffer.
        /// <code>
        /// using (var scope = _ringBuffer.TryPublishEvent(2))
        /// {
        ///     if (!scope.TryGetEvents(out var eventsRef))
        ///         return;
        ///
        ///     ref var e1 = ref eventRefs.Event(0);
        ///     ref var e2 = ref eventRefs.Event(1);
        ///     // Do some work with the events.
        /// }
        /// </code>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NullableUnpublishedEventBatchScope TryPublishEvents(int count)
        {
            if ((uint)count > _bufferSize)
                ThrowInvalidPublishCountException();

            var success = TryNext(count, out var endSequence);
            return new NullableUnpublishedEventBatchScope(success ? this : null, endSequence + 1 - count, endSequence);
        }

        /// <summary>
        /// Holds an unpublished sequence number.
        /// Publishes the sequence number on disposing.
        /// </summary>
        public readonly struct UnpublishedEventScope : IDisposable
        {
            private readonly UnmanagedRingBuffer<T> _ringBuffer;
            private readonly long _sequence;

            public UnpublishedEventScope(UnmanagedRingBuffer<T> ringBuffer, long sequence)
            {
                _ringBuffer = ringBuffer;
                _sequence = sequence;
            }

            public long Sequence => _sequence;

            /// <summary>
            /// Gets the event for the associated sequence number.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref T Event() => ref _ringBuffer[_sequence];

            /// <summary>
            /// Publishes the sequence number.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose() => _ringBuffer.Publish(_sequence);
        }

        /// <summary>
        /// Holds an unpublished sequence number batch.
        /// Publishes the sequence numbers on disposing.
        /// </summary>
        public readonly struct UnpublishedEventBatchScope : IDisposable
        {
            private readonly UnmanagedRingBuffer<T> _ringBuffer;
            private readonly long _startSequence;
            private readonly long _endSequence;

            public UnpublishedEventBatchScope(UnmanagedRingBuffer<T> ringBuffer, long startSequence, long endSequence)
            {
                _ringBuffer = ringBuffer;
                _startSequence = startSequence;
                _endSequence = endSequence;
            }

            public long StartSequence => _startSequence;
            public long EndSequence => _endSequence;

            /// <summary>
            /// Gets the event for the associated sequence number.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref T Event(int index) => ref _ringBuffer[_startSequence + index];

            /// <summary>
            /// Publishes the sequence number batch.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose() => _ringBuffer.Publish(_startSequence, _endSequence);
        }

        /// <summary>
        /// Holds an unpublished sequence number.
        /// Publishes the sequence number on disposing.
        /// </summary>
        public readonly struct NullableUnpublishedEventScope : IDisposable
        {
            private readonly UnmanagedRingBuffer<T> _ringBuffer;
            private readonly long _sequence;

            public NullableUnpublishedEventScope(UnmanagedRingBuffer<T> ringBuffer, long sequence)
            {
                _ringBuffer = ringBuffer;
                _sequence = sequence;
            }

            /// <summary>
            /// Returns a value indicating whether the sequence was successfully claimed.
            /// </summary>
            public bool HasEvent => _ringBuffer != null;

            /// <summary>
            /// Gets the event for the associated sequence number.
            /// </summary>
            /// <returns>
            /// true if the sequence number was successfully claimed, false otherwise.
            /// </returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryGetEvent(out EventRef eventRef)
            {
                eventRef = new EventRef(_ringBuffer, _sequence);
                return _ringBuffer != null;
            }

            /// <summary>
            /// Publishes the sequence number if it was successfully claimed.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                if (_ringBuffer != null)
                    _ringBuffer.Publish(_sequence);
            }
        }

        /// <summary>
        /// Holds an unpublished sequence number.
        /// </summary>
        public readonly struct EventRef
        {
            private readonly UnmanagedRingBuffer<T> _ringBuffer;
            private readonly long _sequence;

            public EventRef(UnmanagedRingBuffer<T> ringBuffer, long sequence)
            {
                _ringBuffer = ringBuffer;
                _sequence = sequence;
            }

            public long Sequence => _sequence;

            /// <summary>
            /// Gets the event for the associated sequence number.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref T Event() => ref _ringBuffer[_sequence];
        }

        /// <summary>
        /// Holds an unpublished sequence number batch.
        /// Publishes the sequence numbers on disposing.
        /// </summary>
        public readonly struct NullableUnpublishedEventBatchScope : IDisposable
        {
            private readonly UnmanagedRingBuffer<T> _ringBuffer;
            private readonly long _startSequence;
            private readonly long _endSequence;

            public NullableUnpublishedEventBatchScope(UnmanagedRingBuffer<T> ringBuffer, long startSequence, long endSequence)
            {
                _ringBuffer = ringBuffer;
                _startSequence = startSequence;
                _endSequence = endSequence;
            }

            /// <summary>
            /// Returns a value indicating whether the sequence batch was successfully claimed.
            /// </summary>
            public bool HasEvents => _ringBuffer != null;

            /// <summary>
            /// Gets the events for the associated sequence number batch.
            /// </summary>
            /// <returns>
            /// true if the sequence batch was successfully claimed, false otherwise.
            /// </returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryGetEvents(out EventBatchRef eventRef)
            {
                eventRef = new EventBatchRef(_ringBuffer, _startSequence, _endSequence);
                return _ringBuffer != null;
            }

            /// <summary>
            /// Publishes the sequence batch if it was successfully claimed.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                if (_ringBuffer != null)
                    _ringBuffer.Publish(_startSequence, _endSequence);
            }
        }

        /// <summary>
        /// Holds an unpublished sequence number batch.
        /// </summary>
        public readonly struct EventBatchRef
        {
            private readonly UnmanagedRingBuffer<T> _ringBuffer;
            private readonly long _startSequence;
            private readonly long _endSequence;

            public EventBatchRef(UnmanagedRingBuffer<T> ringBuffer, long startSequence, long endSequence)
            {
                _ringBuffer = ringBuffer;
                _startSequence = startSequence;
                _endSequence = endSequence;
            }

            public long StartSequence => _startSequence;
            public long EndSequence => _endSequence;

            /// <summary>
            /// Gets the event for the associated sequence number and the specified index.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref T Event(int index) => ref _ringBuffer[_startSequence + index];
        }
    }
}
