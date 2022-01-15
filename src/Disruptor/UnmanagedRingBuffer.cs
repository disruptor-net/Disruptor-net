using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor.Util;
using static Disruptor.Util.Constants;

namespace Disruptor;

/// <summary>
/// Base type for unmanaged-memory-backed ring buffers.
///
/// <see cref="UnmanagedRingBuffer{T}"/>.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = DefaultPadding * 2 + 40)]
public abstract class UnmanagedRingBuffer : ICursored
{
    // padding: DefaultPadding

    [FieldOffset(DefaultPadding)]
    protected IntPtr _entries;

    [FieldOffset(DefaultPadding + 8)]
    protected long _indexMask;

    [FieldOffset(DefaultPadding + 16)]
    protected int _eventSize;

    [FieldOffset(DefaultPadding + 20)]
    protected int _bufferSize;

    [FieldOffset(DefaultPadding + 24)]
    protected SequencerDispatcher _sequencerDispatcher; // includes 7 bytes of padding

    // padding: DefaultPadding

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
}