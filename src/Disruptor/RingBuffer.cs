﻿using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor.Util;
using static Disruptor.Util.Constants;

namespace Disruptor;

/// <summary>
/// Base type for array-backed ring buffers.
/// </summary>
/// <seealso cref="RingBuffer{T}"/>
/// <seealso cref="ValueRingBuffer{T}"/>.
[StructLayout(LayoutKind.Explicit, Size = DefaultPadding * 2 + 40)]
public abstract class RingBuffer : ICursored
{
    protected static readonly int _bufferPadRef = InternalUtil.GetRingBufferPaddingEventCount(IntPtr.Size);

    // padding: DefaultPadding

    [FieldOffset(DefaultPadding)]
    protected object _entries;

    [FieldOffset(DefaultPadding + 8)]
    protected long _indexMask;

    [FieldOffset(DefaultPadding + 16)]
    protected int _bufferSize;

    [FieldOffset(DefaultPadding + 24)]
    protected SequencerDispatcher _sequencerDispatcher; // includes 7 bytes of padding

    // padding: DefaultPadding

    /// <summary>
    /// Construct a RingBuffer with the full option set.
    /// </summary>
    /// <param name="sequencer">sequencer to handle the ordering of events moving through the RingBuffer.</param>
    /// <param name="eventType">type of ring buffer events</param>
    /// <param name="bufferPad">ring buffer padding  as a number of events</param>
    /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
    protected RingBuffer(ISequencer sequencer, Type eventType, int bufferPad)
    {
        _sequencerDispatcher = new SequencerDispatcher(sequencer);
        _bufferSize = sequencer.BufferSize;

        if (_bufferSize < 1)
        {
            throw new ArgumentException("bufferSize must not be less than 1");
        }
        if (!_bufferSize.IsPowerOf2())
        {
            throw new ArgumentException("bufferSize must be a power of 2");
        }

        _entries = Array.CreateInstance(eventType, _bufferSize + 2 * bufferPad);
        _indexMask = _bufferSize - 1;
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
        return _sequencerDispatcher.Next();
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
        if (n < 1 || n > _bufferSize)
        {
            ThrowHelper.ThrowArgMustBeGreaterThanZeroAndLessThanBufferSize();
        }

        return _sequencerDispatcher.Next(n);
    }

    /// <summary>
    /// Try to claim an available sequence in the ring buffer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Calls of this method should ensure that they always publish the sequence afterward.
    /// </para>
    /// <para>
    /// If there is not enough space available in the ring buffer, this method will return false.
    /// </para>
    /// <para>
    /// Example:
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
    /// </para>
    /// </remarks>
    /// <param name="sequence">the next sequence to publish to</param>
    /// <returns>true if the necessary space in the ring buffer is not available, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryNext(out long sequence)
    {
        return _sequencerDispatcher.TryNext(out sequence);
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
        if (n < 1 || n > _bufferSize)
        {
            ThrowHelper.ThrowArgMustBeGreaterThanZeroAndLessThanBufferSize();
        }

        return _sequencerDispatcher.TryNext(n, out sequence);
    }

    /// <summary>
    /// Add the specified gating sequences to this instance of the Disruptor.  They will
    /// safely and atomically added to the list of gating sequences.
    /// </summary>
    /// <param name="gatingSequences">the sequences to add.</param>
    public void AddGatingSequences(params Sequence[] gatingSequences)
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
    public bool RemoveGatingSequence(Sequence sequence)
    {
        return _sequencerDispatcher.Sequencer.RemoveGatingSequence(sequence);
    }

    /// <summary>
    /// Create a new sequence barrier to be used by an event processor to track which messages
    /// are available to be read from the ring buffer given a list of sequences to track.
    /// </summary>
    /// <param name="eventHandler">The event handler of the target event processor. Can be null for custom event processors or if the event processor is a <see cref="IWorkHandler{T}"/> processor.</param>
    /// <param name="sequencesToTrack">the additional sequences to track</param>
    /// <returns>A sequence barrier that will track the specified sequences.</returns>
    public SequenceBarrier NewBarrier(IEventHandler eventHandler, params Sequence[] sequencesToTrack)
    {
        return NewBarrier(SequenceWaiterOwner.EventHandler(eventHandler), sequencesToTrack);
    }

    /// <summary>
    /// Create a new sequence barrier to be used by an event processor to track which messages
    /// are available to be read from the ring buffer given a list of sequences to track.
    /// </summary>
    /// <param name="owner">The owner of the sequence waiter.</param>
    /// <param name="sequencesToTrack">the additional sequences to track</param>
    /// <returns>A sequence barrier that will track the specified sequences.</returns>
    public SequenceBarrier NewBarrier(SequenceWaiterOwner owner, params Sequence[] sequencesToTrack)
    {
        return _sequencerDispatcher.Sequencer.NewBarrier(owner, sequencesToTrack);
    }

    /// <inheritdoc cref="ICursored.Cursor"/>.
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

    /// <summary>
    /// Determines if the event for a given sequence is currently available.
    /// <para>
    /// Note that this does not guarantee that event will still be available
    /// on the next interaction with the RingBuffer. For example, it is not
    /// necessarily safe to write code like this:
    /// <code>
    /// if (ringBuffer.IsAvailable(sequence))
    /// {
    ///     var e = ringBuffer[sequence];
    ///     // ...do something with e
    /// }
    /// </code>
    /// because there is a race between the reading thread and the writing thread.
    /// </para>
    /// <para>
    /// This method will also return false when querying for sequences that are
    /// behind the ring buffer's wrap point.
    /// </para>
    /// </summary>
    /// <param name="sequence">The sequence to identify the entry.</param>
    /// <returns>If the event published with the given sequence number is currently available.</returns>
    public bool IsAvailable(long sequence)
    {
        return _sequencerDispatcher.Sequencer.IsAvailable(sequence);
    }
}
