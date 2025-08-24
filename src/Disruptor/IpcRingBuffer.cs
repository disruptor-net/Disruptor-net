using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Disruptor.Dsl;
using Disruptor.Processing;
using Disruptor.Util;
using static Disruptor.Util.Constants;

namespace Disruptor;

[StructLayout(LayoutKind.Explicit, Size = DefaultPadding * 2 + 48)]
internal unsafe struct IpcRingBufferFields
{
    // padding: DefaultPadding

    [FieldOffset(DefaultPadding)]
    public byte* RingBuffer;

    [FieldOffset(DefaultPadding + 8)]
    public long IndexMask;

    [FieldOffset(DefaultPadding + 16)]
    public int BufferSize;

    [FieldOffset(DefaultPadding + 20)]
    public bool OwnsMemory;

    [FieldOffset(DefaultPadding + 24)]
    public IpcSequencer Sequencer;

    [FieldOffset(DefaultPadding + 32)]
    public string IpcDirectoryPath;

    [FieldOffset(DefaultPadding + 40)]
    public IpcRingBufferMemory? Memory;

    // padding: DefaultPadding
}

/// <summary>
/// Ring based store of reusable entries containing the data representing
/// an event being exchanged between event producer and <see cref="IEventProcessor"/>s.
///
/// The underlying storage is a shared memory buffer. The buffer must be preallocated.
/// </summary>
/// <typeparam name="T">implementation storing the data for sharing during exchange or parallel coordination of an event.</typeparam>
public sealed unsafe class IpcRingBuffer<T> : ISequenced, ICursored, IDisposable
    where T : unmanaged
{
    private IpcRingBufferFields _fields;

    /// <summary>
    /// Creates a ring buffer with the specified memory and wait strategy.
    /// </summary>
    /// <param name="memory">the IPC shared memory</param>
    /// <param name="waitStrategy">used to determine how to wait for new elements to become available.</param>
    /// <param name="ownsMemory">indicates whether the memory should be disposed when the ring buffer is disposed</param>
    public IpcRingBuffer(IpcRingBufferMemory memory, IIpcWaitStrategy waitStrategy, bool ownsMemory)
    {
        memory.RegisterRingBuffer();

        _fields = new IpcRingBufferFields
        {
            RingBuffer = memory.RingBuffer,
            IndexMask = memory.BufferSize - 1,
            BufferSize = memory.BufferSize,
            Sequencer = new IpcSequencer(memory, waitStrategy),
            IpcDirectoryPath = memory.IpcDirectoryPath,
            Memory = memory,
            OwnsMemory = ownsMemory,
        };
    }

    public void Dispose()
    {
        var memory = Interlocked.Exchange(ref _fields.Memory, null);
        if (memory == null)
            return;

        memory.UnregisterRingBuffer();

        if (_fields.OwnsMemory)
        {
            memory.Dispose();
        }
    }

    public string IpcDirectoryPath => _fields.IpcDirectoryPath;

    public int BufferSize => _fields.BufferSize;

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
    ///     ref var eventToPublish = ref ringBuffer[sequence];
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
    public ref T this[long sequence]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.AsRef<T>((T*)(_fields.RingBuffer + (int)(sequence & _fields.IndexMask) * sizeof(T)));
    }

    public override string ToString()
    {
        return $"IpcRingBuffer {{Type={typeof(T).Name}, BufferSize={_fields.BufferSize}}}";
    }

    /// <summary>
    /// Increment the ring buffer sequence and return a scope that will publish the sequence on disposing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If there is not enough space available in the ring buffer, this method will block and spin-wait using <see cref="AggressiveSpinWait"/>, which can generate high CPU usage.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// using (var scope = _ringBuffer.PublishEvent())
    /// {
    ///     ref var e = ref scope.Event();
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
    /// Increment the ring buffer sequence by <paramref name="count"/> and return a scope that will publish the sequences on disposing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If there is not enough space available in the ring buffer, this method will block and spin-wait using <see cref="AggressiveSpinWait"/>, which can generate high CPU usage.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// using (var scope = _ringBuffer.PublishEvents(2))
    /// {
    ///     ref var e1 = ref scope.Event(0);
    ///     ref var e2 = ref scope.Event(1);
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
    /// Given specified <paramref name="requiredCapacity"/> determines if that amount of space
    /// is available.  Note, you can not assume that if this method returns <c>true</c>
    /// that a call to <see cref="Next()"/> will not block.  Especially true if this
    /// ring buffer is set up to handle multiple producers.
    /// </summary>
    /// <param name="requiredCapacity">The capacity to check for.</param>
    /// <returns><c>true</c> if the specified <paramref name="requiredCapacity"/> is available <c>false</c> if not.</returns>
    public bool HasAvailableCapacity(int requiredCapacity)
    {
        return _fields.Sequencer.HasAvailableCapacity(requiredCapacity);
    }

    /// <summary>
    /// Claim an available sequence in the ring buffer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Calls of this method should ensure that they always publish the sequence afterward.
    /// </para>
    /// <para>
    /// If there is not enough space available in the ring buffer, this method will block and spin-wait using <see cref="AggressiveSpinWait"/>, which can generate high CPU usage.
    /// Consider using <see cref="TryNext(out long)"/> with your own waiting policy if you need to change this behavior.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// long sequence = ringBuffer.Next();
    /// try
    /// {
    ///     // Do some work with ringBuffer[sequence];
    /// }
    /// finally
    /// {
    ///     ringBuffer.Publish(sequence);
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    /// <returns>The claimed sequence number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Next()
    {
        return _fields.Sequencer.Next();
    }

    /// <summary>
    /// Claim a range of <paramref name="n"/> available sequences in the ring buffer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Calls of this method should ensure that they always publish the sequences afterward.
    /// </para>
    /// <para>
    /// If there is not enough space available in the ring buffer, this method will block and spin-wait using <see cref="AggressiveSpinWait"/>, which can generate high CPU usage.
    /// Consider using <see cref="TryNext(int, out long)"/> with your own waiting policy if you need to change this behavior.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// long hi = ringBuffer.Next(_batchSize);
    /// long lo = hi - _batchSize + 1;
    /// try
    /// {
    ///     for (long s = lo; s &lt;= hi; s++)
    ///     {
    ///         // Do some work with ringBuffer[s];
    ///     }
    /// }
    /// finally
    /// {
    ///     ringBuffer.Publish(lo, hi);
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    /// <param name="n">number of slots to claim</param>
    /// <returns>The sequence number of the highest slot claimed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Next(int n)
    {
        return _fields.Sequencer.Next(n);
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
    ///     // Do some work with ringBuffer[sequence];
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
        return _fields.Sequencer.TryNext(out sequence);
    }

    /// <summary>
    /// Try to claim a range of <paramref name="n"/> available sequences in the ring buffer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Calls of this method should ensure that they always publish the sequences afterward.
    /// </para>
    /// <para>
    /// If there is not enough space available in the ring buffer, this method will return false.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// if (!ringBuffer.TryNext(_batchSize, out var hi))
    /// {
    ///     // Handle full ring buffer
    ///     return;
    /// }
    /// long lo = hi - _batchSize + 1;
    /// try
    /// {
    ///     for (long s = lo; s &lt;= hi; s++)
    ///     {
    ///         // Do some work with ringBuffer[s];
    ///     }
    /// }
    /// finally
    /// {
    ///     ringBuffer.Publish(lo, hi);
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    /// <param name="n">number of slots to claim</param>
    /// <param name="sequence">sequence number of the highest slot claimed</param>
    /// <returns>true if the necessary space in the ring buffer is not available, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryNext(int n, out long sequence)
    {
        return _fields.Sequencer.TryNext(n, out sequence);
    }

    internal void SetGatingSequences(params SequencePointer[] gatingSequences)
    {
        _fields.Sequencer.SetGatingSequences(gatingSequences);
    }

    internal SequencePointer[] GetGatingSequences()
    {
        return _fields.Sequencer.GetGatingSequences();
    }

    /// <summary>
    /// Get the minimum sequence value from all of the gating sequences
    /// added to this ringBuffer.
    /// </summary>
    /// <returns>the minimum gating sequence or the cursor sequence if no sequences have been added.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetMinimumGatingSequence()
    {
        return _fields.Sequencer.GetMinimumSequence();
    }

    internal SequencePointer NewSequence()
    {
        return _fields.Sequencer.NewSequence();
    }

    internal IpcSequenceBarrier NewBarrier(SequenceWaiterOwner owner, SequencePointer[] sequencesToTrack)
    {
        return _fields.Sequencer.NewBarrier(owner, sequencesToTrack);
    }

    /// <summary>
    /// Get the current cursor value for the ring buffer.  The actual value received
    /// will depend on the type of <see cref="ISequencer"/> that is being used.
    /// </summary>
    public long Cursor => _fields.Sequencer.Cursor;

    /// <summary>
    /// Publish the specified sequence.  This action marks this particular
    /// message as being available to be read.
    /// </summary>
    /// <param name="sequence">the sequence to publish.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish(long sequence)
    {
        _fields.Sequencer.Publish(sequence);
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
        _fields.Sequencer.Publish(lo, hi);
    }

    /// <summary>
    /// Get the remaining capacity for this ringBuffer.
    /// </summary>
    /// <returns>The number of slots remaining.</returns>
    public long GetRemainingCapacity()
    {
        return _fields.Sequencer.GetRemainingCapacity();
    }

    /// <summary>
    /// Holds an unpublished sequence number.
    /// Publishes the sequence number on disposing.
    /// </summary>
    public readonly struct UnpublishedEventScope : IDisposable
    {
        private readonly IpcRingBuffer<T> _ringBuffer;
        private readonly long _sequence;

        public UnpublishedEventScope(IpcRingBuffer<T> ringBuffer, long sequence)
        {
            _ringBuffer = ringBuffer;
            _sequence = sequence;
        }

        public long Sequence => _sequence;

        /// <summary>
        /// Gets the event at the claimed sequence number.
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
        private readonly IpcRingBuffer<T> _ringBuffer;
        private readonly long _startSequence;
        private readonly long _endSequence;

        public UnpublishedEventBatchScope(IpcRingBuffer<T> ringBuffer, long startSequence, long endSequence)
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
        public ref T Event(int index) => ref _ringBuffer[_startSequence + index];

        /// <summary>
        /// Publishes the sequence number batch.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => _ringBuffer.Publish(_startSequence, _endSequence);
    }
}
