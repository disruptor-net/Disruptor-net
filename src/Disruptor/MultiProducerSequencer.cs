using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Disruptor.Dsl;
using Disruptor.Util;

namespace Disruptor;

/// <summary>
/// <para>Coordinator for claiming sequences for access to a data structure while tracking dependent <see cref="Sequence"/>s.
/// Suitable for use for sequencing across multiple publisher threads.</para>
/// <para/>
/// <para/>Note on <see cref="ICursored.Cursor"/>:  With this sequencer the cursor value is updated after the call
/// to <see cref="ISequenced.Next()"/>, to determine the highest available sequence that can be read, then
/// <see cref="GetHighestPublishedSequence"/> should be used.
/// </summary>
public sealed unsafe class MultiProducerSequencer : ISequencer
{
    private SequencerCore _sequencerCore;
    [SuppressMessage("ReSharper", "PrivateFieldCanBeConvertedToLocalVariable", Justification = "Prevents the GC from collecting the array")]
    // availableBuffer tracks the state of each ringbuffer slot
    // see below for more details on the approach:
    // <p>
    // The prime reason is to avoid a shared sequence object between publisher threads.
    // (Keeping single pointers tracking start and end would require coordination
    // between the threads).
    // <p>
    // --  Firstly we have the constraint that the delta between the cursor and minimum
    // gating sequence will never be larger than the buffer size (the code in
    // next/tryNext in the Sequence takes care of that).
    // -- Given that; take the sequence value and mask off the lower portion of the
    // sequence as the index into the buffer (indexMask). (aka modulo operator)
    // -- The upper portion of the sequence becomes the value to check for availability.
    // ie: it tells us how many times around the ring buffer we've been (aka division)
    // -- Because we can't wrap without the gating sequences moving forward (i.e. the
    // minimum gating sequence is effectively our last available position in the
    // buffer), when we have new data and successfully claimed a slot we can simply
    // write over the top.
    private readonly int[] _availableBuffer;

#if NETCOREAPP
    private readonly int* _availableBufferPointer;
#endif

    private readonly int _indexMask;
    private readonly int _indexShift;

    // Java uses a reference type here, but it is not required because _gatingSequenceCache is not exposed
    // outside MultiProducerSequencer, so a value type is more efficient.
    private SequenceCache _gatingSequenceCache;

    public MultiProducerSequencer(int bufferSize)
        : this(bufferSize, SequencerFactory.DefaultWaitStrategy())
    {
    }

    public MultiProducerSequencer(int bufferSize, IWaitStrategy waitStrategy)
    {
        _sequencerCore = new SequencerCore(bufferSize, waitStrategy);
#if NETCOREAPP
        _availableBuffer = GC.AllocateArray<int>(bufferSize, pinned: true);
        _availableBufferPointer = (int*)Unsafe.AsPointer(ref _availableBuffer[0]);
#else
        _availableBuffer = new int[bufferSize];
#endif
        _indexMask = bufferSize - 1;
        _indexShift = DisruptorUtil.Log2(bufferSize);

        _availableBuffer.AsSpan().Fill(-1);
    }

    /// <inheritdoc/>
    public SequenceBarrier NewBarrier(SequenceWaiterOwner owner, params Sequence[] sequencesToTrack)
    {
        var sequenceWaiter = _sequencerCore.NewSequenceWaiter(owner, sequencesToTrack);

        return new SequenceBarrier(this, sequenceWaiter);
    }

    /// <inheritdoc/>
    public AsyncSequenceBarrier NewAsyncBarrier(SequenceWaiterOwner owner, params Sequence[] sequencesToTrack)
    {
        var sequenceWaiter = _sequencerCore.NewAsyncSequenceWaiter(owner, sequencesToTrack);

        return new AsyncSequenceBarrier(this, sequenceWaiter);
    }

    /// <inheritdoc/>
    public int BufferSize => _sequencerCore.BufferSize;

    /// <inheritdoc/>
    public long Cursor => _sequencerCore.CursorPointer.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAvailableCapacity(int requiredCapacity)
    {
        return HasAvailableCapacity(requiredCapacity, _sequencerCore.CursorPointer.Value);
    }

    private bool HasAvailableCapacity(int requiredCapacity, long cursorValue)
    {
        var wrapPoint = (cursorValue + requiredCapacity) - _sequencerCore.BufferSize;
        var cachedGatingSequence = _gatingSequenceCache.Value;

        if (wrapPoint > cachedGatingSequence || cachedGatingSequence > cursorValue)
        {
            var minSequence = DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _sequencerCore.GatingSequencePointers), cursorValue);
            _gatingSequenceCache.SetValue(minSequence);

            if (wrapPoint > minSequence)
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Claim(long sequence)
    {
        _sequencerCore.CursorPointer.SetValue(sequence);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Next()
    {
        return NextInternal(1);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Next(int n)
    {
        if ((uint)(n - 1) >= _sequencerCore.BufferSize)
        {
            ThrowHelper.ThrowArgMustBeGreaterThanZeroAndLessThanBufferSize();
        }

        return NextInternal(n);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal long NextInternal(int n)
    {
        var next = _sequencerCore.CursorPointer.AddAndGet(n);
        var current = next - n;
        var wrapPoint = next - _sequencerCore.BufferSize;
        var cachedGatingSequence = _gatingSequenceCache.Value;

        if (wrapPoint > cachedGatingSequence || cachedGatingSequence > current)
        {
            NextInternalOnWrapPointReached(wrapPoint, current);
        }

        return next;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void NextInternalOnWrapPointReached(long wrapPoint, long current)
    {
        var spinWait = default(AggressiveSpinWait);
        long minSequence;
        while (wrapPoint > (minSequence = DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _sequencerCore.GatingSequencePointers), current)))
        {
            spinWait.SpinOnce();
        }

        _gatingSequenceCache.SetValue(minSequence);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryNext(out long sequence)
    {
        return TryNextInternal(1, out sequence);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryNext(int n, out long sequence)
    {
        if (n < 1 || n > _sequencerCore.BufferSize)
        {
            ThrowHelper.ThrowArgMustBeGreaterThanZeroAndLessThanBufferSize();
        }

        return TryNextInternal(n, out sequence);
    }

    internal bool TryNextInternal(int n, out long sequence)
    {
        long current;
        long next;

        do
        {
            current = _sequencerCore.CursorPointer.Value;
            next = current + n;

            if (!HasAvailableCapacity(n, current))
            {
                sequence = default;
                return false;
            }
        }
        while (!_sequencerCore.CursorPointer.CompareAndSet(current, next));

        sequence = next;
        return true;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetRemainingCapacity()
    {
        var consumed = DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _sequencerCore.GatingSequencePointers), _sequencerCore.CursorPointer.Value);
        var produced = _sequencerCore.CursorPointer.Value;
        return BufferSize - (produced - consumed);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish(long sequence)
    {
        SetAvailableBufferValue(CalculateIndex(sequence), CalculateAvailabilityFlag(sequence));

        if (_sequencerCore.IsBlockingWaitStrategy)
        {
            _sequencerCore.WaitStrategy.SignalAllWhenBlocking();
        }
    }

    /// <inheritdoc/>
    public void Publish(long lo, long hi)
    {
        for (var l = lo; l <= hi; l++)
        {
            SetAvailableBufferValue(CalculateIndex(l), CalculateAvailabilityFlag(l));
        }

        if (_sequencerCore.IsBlockingWaitStrategy)
        {
            _sequencerCore.WaitStrategy.SignalAllWhenBlocking();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetAvailableBufferValue(int index, int flag)
    {
#if NETCOREAPP
        Volatile.Write(ref _availableBufferPointer[index], flag);
#else
        Volatile.Write(ref _availableBuffer[index], flag);
#endif
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAvailable(long sequence)
    {
        var index = CalculateIndex(sequence);
        var flag = CalculateAvailabilityFlag(sequence);

#if NETCOREAPP
        return Volatile.Read(ref _availableBufferPointer[index]) == flag;
#else
        return Volatile.Read(ref _availableBuffer[index]) == flag;
#endif
    }

    /// <inheritdoc/>
    public long GetHighestPublishedSequence(long lowerBound, long availableSequence)
    {
        for (var sequence = lowerBound; sequence <= availableSequence; sequence++)
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
        return (int)sequence & _indexMask;
    }

    /// <inheritdoc/>
    public void AddGatingSequences(params Sequence[] gatingSequences)
    {
        _sequencerCore.AddGatingSequences(gatingSequences);
    }

    /// <inheritdoc/>
    public bool RemoveGatingSequence(Sequence sequence)
    {
        return _sequencerCore.RemoveGatingSequence(sequence);
    }

    /// <inheritdoc/>
    public long GetMinimumSequence()
    {
        return DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _sequencerCore.GatingSequencePointers), _sequencerCore.CursorPointer.Value);
    }

    /// <inheritdoc/>
    public EventPoller<T> NewPoller<T>(IDataProvider<T> provider, params Sequence[] gatingSequences)
        where T : class
    {
        return EventPoller.Create(provider, this, new Sequence(), _sequencerCore.Cursor, gatingSequences);
    }

    /// <inheritdoc/>
    public ValueEventPoller<T> NewPoller<T>(IValueDataProvider<T> provider, params Sequence[] gatingSequences)
        where T : struct
    {
        return EventPoller.Create(provider, this, new Sequence(), _sequencerCore.Cursor, gatingSequences);
    }

    /// <inheritdoc/>
    public AsyncEventStream<T> NewAsyncEventStream<T>(IDataProvider<T> provider, Sequence[] gatingSequences)
        where T : class
    {
        var sequenceWaiter = _sequencerCore.NewAsyncSequenceWaiter(SequenceWaiterOwner.Unknown, gatingSequences);

        return new AsyncEventStream<T>(provider, sequenceWaiter, this);
    }

    internal Sequence GetCursorSequence() => _sequencerCore.Cursor;

    internal IWaitStrategy GetWaitStrategy() => _sequencerCore.WaitStrategy;

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    private struct SequenceCache
    {
        [FieldOffset(64)]
        private long _value;

        public long Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(ref _value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetValue(long value)
        {
            Volatile.Write(ref _value, value);
        }
    }
}
