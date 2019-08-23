using System;
using System.Runtime.CompilerServices;
using Disruptor.Dsl;

namespace Disruptor
{
    /// <summary>
    /// Ring based store of reusable entries containing the data representing
    /// an event being exchanged between event producer and <see cref="IEventProcessor"/>s.
    /// </summary>
    /// <typeparam name="T">implementation storing the data for sharing during exchange or parallel coordination of an event.</typeparam>
    public sealed class ValueRingBuffer<T> : RingBuffer, ISequenced, IValueDataProvider<T>
        where T : struct
    {
        /// <summary>
        /// Construct a ValueRingBuffer with the full option set.
        /// </summary>
        /// <param name="sequencer">sequencer to handle the ordering of events moving through the ring buffer.</param>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public ValueRingBuffer(ISequencer sequencer)
            : this(() => default(T), sequencer)
        {
        }

        /// <summary>
        /// Construct a ValueRingBuffer with the full option set.
        /// </summary>
        /// <param name="eventFactory">eventFactory to create entries for filling the ring buffer</param>
        /// <param name="sequencer">sequencer to handle the ordering of events moving through the ring buffer.</param>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public ValueRingBuffer(Func<T> eventFactory, ISequencer sequencer)
        : base(sequencer, typeof(T))
        {
            Fill(eventFactory);
        }

        private void Fill(Func<T> eventFactory)
        {
            var entries = (T[]) _entries;
            for (int i = 0; i < _bufferSize; i++)
            {
                entries[_bufferPad + i] = eventFactory();
            }
        }

        /// <summary>
        /// Construct a ValueRingBuffer with a <see cref="MultiProducerSequencer"/> sequencer.
        /// </summary>
        /// <param name="eventFactory"> eventFactory to create entries for filling the ring buffer</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        public ValueRingBuffer(Func<T> eventFactory, int bufferSize)
            : this(eventFactory, new MultiProducerSequencer(bufferSize, new BlockingWaitStrategy()))
        {
        }

        /// <summary>
        /// Create a new multiple producer ValueRingBuffer with the specified wait strategy.
        /// </summary>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <param name="waitStrategy">used to determine how to wait for new elements to become available.</param>
        /// <returns>a constructed ring buffer.</returns>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public static ValueRingBuffer<T> CreateMultiProducer(Func<T> factory, int bufferSize, IWaitStrategy waitStrategy)
        {
            MultiProducerSequencer sequencer = new MultiProducerSequencer(bufferSize, waitStrategy);

            return new ValueRingBuffer<T>(factory, sequencer);
        }

        /// <summary>
        /// Create a new multiple producer ValueRingBuffer using the default wait strategy <see cref="BlockingWaitStrategy"/>.
        /// </summary>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <returns>a constructed ring buffer.</returns>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public static ValueRingBuffer<T> CreateMultiProducer(Func<T> factory, int bufferSize)
        {
            return CreateMultiProducer(factory, bufferSize, new BlockingWaitStrategy());
        }

        /// <summary>
        /// Create a new single producer ValueRingBuffer with the specified wait strategy.
        /// </summary>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <param name="waitStrategy">used to determine how to wait for new elements to become available.</param>
        /// <returns>a constructed ring buffer.</returns>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public static ValueRingBuffer<T> CreateSingleProducer(Func<T> factory, int bufferSize, IWaitStrategy waitStrategy)
        {
            SingleProducerSequencer sequencer = new SingleProducerSequencer(bufferSize, waitStrategy);

            return new ValueRingBuffer<T>(factory, sequencer);
        }

        /// <summary>
        /// Create a new single producer ValueRingBuffer using the default wait strategy <see cref="BlockingWaitStrategy"/>.
        /// </summary>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <returns>a constructed ring buffer.</returns>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public static ValueRingBuffer<T> CreateSingleProducer(Func<T> factory, int bufferSize)
        {
            return CreateSingleProducer(factory, bufferSize, new BlockingWaitStrategy());
        }

        /// <summary>
        /// Create a new ValueRingBuffer with the specified producer type.
        /// </summary>
        /// <param name="producerType">producer type to use <see cref="ProducerType" /></param>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <param name="waitStrategy">used to determine how to wait for new elements to become available.</param>
        /// <returns>a constructed ring buffer.</returns>
        /// <exception cref="ArgumentOutOfRangeException">if the producer type is invalid</exception>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public static ValueRingBuffer<T> Create(ProducerType producerType, Func<T> factory, int bufferSize, IWaitStrategy waitStrategy)
        {
            switch (producerType)
            {
                case ProducerType.Single:
                    return CreateSingleProducer(factory, bufferSize, waitStrategy);
                case ProducerType.Multi:
                    return CreateMultiProducer(factory, bufferSize, waitStrategy);
                default:
                    throw new ArgumentOutOfRangeException(producerType.ToString());
            }
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
                return ref Util.ReadValue<T>(_entries, _bufferPad + (int)(sequence & _indexMask));
            }
        }
        
        /// <summary>
        /// Sets the cursor to a specific sequence and returns the preallocated entry that is stored there.  This
        /// can cause a data race and should only be done in controlled circumstances, e.g. during initialisation.
        /// </summary>
        /// <param name="sequence">the sequence to claim.</param>
        /// <returns>the preallocated event.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T ClaimAndGetPreallocated(long sequence)
        {
            _sequencer.Claim(sequence);
            return ref this[sequence];
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowInvalidPublishCountException()
        {
            throw new ArgumentException($"Invalid publish count: It should be >= 0 and <= {_bufferSize}");
        }

        /// <summary>
        /// Holds an unpublished sequence number.
        /// Publishes the sequence number on disposing.
        /// </summary>
        public readonly struct UnpublishedEventScope : IDisposable
        {
            private readonly ValueRingBuffer<T> _ringBuffer;
            private readonly long _sequence;

            public UnpublishedEventScope(ValueRingBuffer<T> ringBuffer, long sequence)
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
            private readonly ValueRingBuffer<T> _ringBuffer;
            private readonly long _startSequence;
            private readonly long _endSequence;

            public UnpublishedEventBatchScope(ValueRingBuffer<T> ringBuffer, long startSequence, long endSequence)
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
            private readonly ValueRingBuffer<T> _ringBuffer;
            private readonly long _sequence;

            public NullableUnpublishedEventScope(ValueRingBuffer<T> ringBuffer, long sequence)
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
            private readonly ValueRingBuffer<T> _ringBuffer;
            private readonly long _sequence;

            public EventRef(ValueRingBuffer<T> ringBuffer, long sequence)
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
            private readonly ValueRingBuffer<T> _ringBuffer;
            private readonly long _startSequence;
            private readonly long _endSequence;

            public NullableUnpublishedEventBatchScope(ValueRingBuffer<T> ringBuffer, long startSequence, long endSequence)
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
            private readonly ValueRingBuffer<T> _ringBuffer;
            private readonly long _startSequence;
            private readonly long _endSequence;

            public EventBatchRef(ValueRingBuffer<T> ringBuffer, long startSequence, long endSequence)
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
