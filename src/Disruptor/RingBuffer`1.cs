using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor.Dsl;
using Disruptor.Processing;
using Disruptor.Util;

namespace Disruptor;

/// <summary>
/// Ring based store of reusable entries containing the data representing
/// an event being exchanged between event producer and <see cref="IEventProcessor"/>s.
/// </summary>
/// <typeparam name="T">implementation storing the data for sharing during exchange or parallel coordination of an event.</typeparam>
public sealed class RingBuffer<T> : RingBuffer, IDataProvider<T>, ISequenced
    where T : class
{
    /// <summary>
    /// Construct a RingBuffer with the full option set.
    /// </summary>
    /// <param name="eventFactory">eventFactory to create entries for filling the RingBuffer</param>
    /// <param name="sequencer">sequencer to handle the ordering of events moving through the RingBuffer.</param>
    /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
    public RingBuffer(Func<T> eventFactory, ISequencer sequencer)
        : base(sequencer, new T[sequencer.BufferSize + 2 * _bufferPadRef])
    {
        Fill(eventFactory);
    }

    private void Fill(Func<T> eventFactory)
    {
        var entries = (T[])_entries;
        for (int i = 0; i < _bufferSize; i++)
        {
            entries[_bufferPadRef + i] = eventFactory();
        }
    }

    /// <summary>
    /// Construct a RingBuffer with a <see cref="MultiProducerSequencer"/> sequencer.
    /// </summary>
    /// <param name="eventFactory"> eventFactory to create entries for filling the RingBuffer</param>
    /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
    public RingBuffer(Func<T> eventFactory, int bufferSize)
        : this(eventFactory, new MultiProducerSequencer(bufferSize))
    {
    }

    /// <summary>
    /// Create a new multiple producer RingBuffer with the specified wait strategy.
    /// </summary>
    /// <param name="factory">used to create the events within the ring buffer.</param>
    /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
    /// <param name="waitStrategy">used to determine how to wait for new elements to become available.</param>
    /// <returns>a constructed ring buffer.</returns>
    /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
    public static RingBuffer<T> CreateMultiProducer(Func<T> factory, int bufferSize, IWaitStrategy waitStrategy)
    {
        MultiProducerSequencer sequencer = new MultiProducerSequencer(bufferSize, waitStrategy);

        return new RingBuffer<T>(factory, sequencer);
    }

    /// <summary>
    /// Create a new multiple producer RingBuffer using the default wait strategy <see cref="BlockingWaitStrategy"/>.
    /// </summary>
    /// <param name="factory">used to create the events within the ring buffer.</param>
    /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
    /// <returns>a constructed ring buffer.</returns>
    /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
    public static RingBuffer<T> CreateMultiProducer(Func<T> factory, int bufferSize)
    {
        return CreateMultiProducer(factory, bufferSize, SequencerFactory.DefaultWaitStrategy());
    }

    /// <summary>
    /// Create a new single producer RingBuffer with the specified wait strategy.
    /// </summary>
    /// <param name="factory">used to create the events within the ring buffer.</param>
    /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
    /// <param name="waitStrategy">used to determine how to wait for new elements to become available.</param>
    /// <returns>a constructed ring buffer.</returns>
    /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
    public static RingBuffer<T> CreateSingleProducer(Func<T> factory, int bufferSize, IWaitStrategy waitStrategy)
    {
        SingleProducerSequencer sequencer = new SingleProducerSequencer(bufferSize, waitStrategy);

        return new RingBuffer<T>(factory, sequencer);
    }

    /// <summary>
    /// Create a new single producer RingBuffer using <see cref="SequencerFactory.DefaultWaitStrategy"/>.
    /// </summary>
    /// <param name="factory">used to create the events within the ring buffer.</param>
    /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
    /// <returns>a constructed ring buffer.</returns>
    /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
    public static RingBuffer<T> CreateSingleProducer(Func<T> factory, int bufferSize)
    {
        return CreateSingleProducer(factory, bufferSize, SequencerFactory.DefaultWaitStrategy());
    }

    /// <summary>
    /// Create a new Ring Buffer with the specified producer type (SINGLE or MULTI)
    /// </summary>
    /// <param name="producerType">producer type to use <see cref="ProducerType" /></param>
    /// <param name="factory">used to create the events within the ring buffer.</param>
    /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
    /// <param name="waitStrategy">used to determine how to wait for new elements to become available.</param>
    /// <returns>a constructed ring buffer.</returns>
    /// <exception cref="ArgumentOutOfRangeException">if the producer type is invalid</exception>
    /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
    public static RingBuffer<T> Create(ProducerType producerType, Func<T> factory, int bufferSize, IWaitStrategy waitStrategy)
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
    /// Gets the event for a given sequence in the ring buffer.
    /// </summary>
    /// <param name="sequence">sequence for the event</param>
    /// <remarks>
    /// This method should be used for publishing events to the ring buffer:
    /// <code>
    /// long sequence = ringBuffer.Next();
    /// try
    /// {
    ///     var eventToPublish = ringBuffer[sequence];
    ///     // Configure the event
    /// }
    /// finally
    /// {
    ///     ringBuffer.Publish(sequence);
    /// }
    /// </code>
    ///
    /// This method can also be used for event processing but in most cases the processing is performed
    /// in the provided <see cref="IEventProcessor"/> types or in the event pollers.
    /// </remarks>
    public T this[long sequence]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => InternalUtil.Read<T>(_entries, _bufferPadRef + (int)(sequence & _indexMask));
    }

    internal ReadOnlySpan<T> this[long lo, long hi]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var index1 = (int)(lo & _indexMask);
            var index2 = (int)(hi & _indexMask);
            var length = index1 <= index2 ? (1 + index2 - index1) : (_bufferSize - index1);

            return InternalUtil.ReadSpan<T>(_entries, _bufferPadRef + index1, length);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EventBatch<T> GetBatch(long lo, long hi)
    {
        var index1 = (int)(lo & _indexMask);
        var index2 = (int)(hi & _indexMask);
        var length = index1 <= index2 ? (1 + index2 - index1) : (_bufferSize - index1);

        return new EventBatch<T>(_entries, _bufferPadRef + index1, length);
    }

    /// <summary>
    /// Sets the cursor to a specific sequence and returns the preallocated entry that is stored there.  This
    /// can cause a data race and should only be done in controlled circumstances, e.g. during initialisation.
    /// </summary>
    /// <param name="sequence">the sequence to claim.</param>
    /// <returns>the preallocated event.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T ClaimAndGetPreallocated(long sequence)
    {
        _sequencerDispatcher.Sequencer.Claim(sequence);
        return this[sequence];
    }

    public override string ToString()
    {
        return $"RingBuffer {{Type={typeof(T).Name}, BufferSize={_bufferSize}, Sequencer={_sequencerDispatcher.Sequencer.GetType().Name}}}";
    }

    /// <summary>
    /// Creates an event poller for this ring buffer gated on the supplied sequences.
    /// </summary>
    /// <param name="gatingSequences">gatingSequences to be gated on.</param>
    public EventPoller<T> NewPoller(params Sequence[] gatingSequences)
    {
        return _sequencerDispatcher.Sequencer.NewPoller(this, gatingSequences);
    }

    /// <summary>
    /// Creates an event stream for this ring buffer gated on the supplied sequences.
    /// </summary>
    /// <param name="gatingSequences">gatingSequences to be gated on.</param>
    public AsyncEventStream<T> NewAsyncEventStream(params Sequence[] gatingSequences)
    {
        return _sequencerDispatcher.Sequencer.NewAsyncEventStream(this, gatingSequences);
    }

    /// <summary>
    /// Create a new sequence barrier to be used by an event processor to track which messages
    /// are available to be read from the ring buffer given a list of sequences to track.
    /// </summary>
    /// <param name="owner">The owner of the sequence waiter.</param>
    /// <param name="sequencesToTrack">the additional sequences to track</param>
    /// <returns>A sequence barrier that will track the specified sequences.</returns>
    public AsyncSequenceBarrier NewAsyncBarrier(SequenceWaiterOwner owner, params Sequence[] sequencesToTrack)
    {
        return _sequencerDispatcher.Sequencer.NewAsyncBarrier(owner, sequencesToTrack);
    }

    /// <summary>
    /// Increment the ring buffer sequence and return a scope that will publish the sequence on disposing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If there is not enough space available in the ring buffer, this method will block and spin-wait using <see cref="AggressiveSpinWait"/>, which can generate high CPU usage.
    /// Consider using <see cref="TryPublishEvent()"/> with your own waiting policy if you need to change this behavior.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// using (var scope = _ringBuffer.PublishEvent())
    /// {
    ///     var e = scope.Event();
    ///     // Do some work with the event.
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnpublishedEventScope PublishEvent()
    {
        var sequence = Next();
        return new UnpublishedEventScope(this, sequence);
    }

    /// <summary>
    /// Try to increment the ring buffer sequence and return a scope that will publish the sequence on disposing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method will not block if there is not enough space available in the ring buffer.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// using (var scope = _ringBuffer.TryPublishEvent())
    /// {
    ///     if (!scope.TryGetEvent(out var eventRef))
    ///         return;
    ///
    ///     var e = eventRef.Event();
    ///     // Do some work with the event.
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NullableUnpublishedEventScope TryPublishEvent()
    {
        var success = TryNext(out var sequence);
        return new NullableUnpublishedEventScope(success ? this : null, sequence);
    }

    /// <summary>
    /// Increment the ring buffer sequence by <paramref name="count"/> and return a scope that will publish the sequences on disposing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If there is not enough space available in the ring buffer, this method will block and spin-wait using <see cref="AggressiveSpinWait"/>, which can generate high CPU usage.
    /// Consider using <see cref="TryPublishEvents(int)"/> with your own waiting policy if you need to change this behavior.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// using (var scope = _ringBuffer.PublishEvents(2))
    /// {
    ///     var e1 = scope.Event(0);
    ///     var e2 = scope.Event(1);
    ///     // Do some work with the events.
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnpublishedEventBatchScope PublishEvents(int count)
    {
        var endSequence = Next(count);
        return new UnpublishedEventBatchScope(this, endSequence + 1 - count, endSequence);
    }

    /// <summary>
    /// Try to increment the ring buffer sequence by <paramref name="count"/> and return a scope that will publish the sequences on disposing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method will not block when there is not enough space available in the ring buffer.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// using (var scope = _ringBuffer.TryPublishEvent(2))
    /// {
    ///     if (!scope.TryGetEvents(out var eventsRef))
    ///         return;
    ///
    ///     var e1 = eventRefs.Event(0);
    ///     var e2 = eventRefs.Event(1);
    ///     // Do some work with the events.
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NullableUnpublishedEventBatchScope TryPublishEvents(int count)
    {
        var success = TryNext(count, out var endSequence);
        return new NullableUnpublishedEventBatchScope(success ? this : null, endSequence + 1 - count, endSequence);
    }

    /// <summary>
    /// Holds an unpublished sequence number.
    /// Publishes the sequence number on disposing.
    /// </summary>
    public readonly struct UnpublishedEventScope : IDisposable
    {
        private readonly RingBuffer<T> _ringBuffer;
        private readonly long _sequence;

        public UnpublishedEventScope(RingBuffer<T> ringBuffer, long sequence)
        {
            _ringBuffer = ringBuffer;
            _sequence = sequence;
        }

        public long Sequence => _sequence;

        /// <summary>
        /// Gets the event at the claimed sequence number.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Event() => _ringBuffer[_sequence];

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
        private readonly RingBuffer<T> _ringBuffer;
        private readonly long _startSequence;
        private readonly long _endSequence;

        public UnpublishedEventBatchScope(RingBuffer<T> ringBuffer, long startSequence, long endSequence)
        {
            _ringBuffer = ringBuffer;
            _startSequence = startSequence;
            _endSequence = endSequence;
        }

        public long StartSequence => _startSequence;
        public long EndSequence => _endSequence;

        /// <summary>
        /// Gets the event at the specified index in the claimed sequence batch.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Event(int index) => _ringBuffer[_startSequence + index];

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
        private readonly RingBuffer<T>? _ringBuffer;
        private readonly long _sequence;

        public NullableUnpublishedEventScope(RingBuffer<T>? ringBuffer, long sequence)
        {
            _ringBuffer = ringBuffer;
            _sequence = sequence;
        }

        /// <summary>
        /// Returns a value indicating whether the sequence was successfully claimed.
        /// </summary>
        public bool HasEvent => _ringBuffer != null;

        /// <summary>
        /// Gets the event at the claimed sequence number.
        /// </summary>
        /// <returns>
        /// true if the sequence number was successfully claimed, false otherwise.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetEvent(out EventRef eventRef)
        {
            eventRef = new EventRef(_ringBuffer!, _sequence);
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
        private readonly RingBuffer<T> _ringBuffer;
        private readonly long _sequence;

        public EventRef(RingBuffer<T> ringBuffer, long sequence)
        {
            _ringBuffer = ringBuffer;
            _sequence = sequence;
        }

        public long Sequence => _sequence;

        /// <summary>
        /// Gets the event at the claimed sequence number.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Event() => _ringBuffer[_sequence];
    }

    /// <summary>
    /// Holds an unpublished sequence number batch.
    /// Publishes the sequence numbers on disposing.
    /// </summary>
    public readonly struct NullableUnpublishedEventBatchScope : IDisposable
    {
        private readonly RingBuffer<T>? _ringBuffer;
        private readonly long _startSequence;
        private readonly long _endSequence;

        public NullableUnpublishedEventBatchScope(RingBuffer<T>? ringBuffer, long startSequence, long endSequence)
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
            eventRef = new EventBatchRef(_ringBuffer!, _startSequence, _endSequence);
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
        private readonly RingBuffer<T> _ringBuffer;
        private readonly long _startSequence;
        private readonly long _endSequence;

        public EventBatchRef(RingBuffer<T> ringBuffer, long startSequence, long endSequence)
        {
            _ringBuffer = ringBuffer;
            _startSequence = startSequence;
            _endSequence = endSequence;
        }

        public long StartSequence => _startSequence;
        public long EndSequence => _endSequence;

        /// <summary>
        /// Gets the event at the specified index in the claimed sequence batch.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Event(int index) => _ringBuffer[_startSequence + index];
    }


    internal RingBufferAccessor GetAccessor() => new RingBufferAccessor(_entries, _indexMask);

    internal readonly ref struct RingBufferAccessor
    {

#if NET8_0_OR_GREATER
        private readonly ref T _start;
#else
        private readonly object _entries;
#endif
        private readonly uint _indexMask;
        public RingBufferAccessor(object entries, long indexMask)
        {
            _indexMask = (uint)indexMask;
#if NET8_0_OR_GREATER
            _start = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Unsafe.As<T[]>(entries)), (uint)_bufferPadRef);
#else
            _entries = entries;
#endif
        }

        public T this[long sequence]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
#if NET8_0_OR_GREATER
                return Unsafe.Add(ref _start, (uint)(sequence & _indexMask));
#else
                return InternalUtil.Read<T>(_entries, _bufferPadRef + (int)(sequence & _indexMask));
#endif
            }
        }
    }
}
