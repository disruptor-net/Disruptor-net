using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
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
internal sealed unsafe class IpcSequencer
{
    private readonly IpcRingBufferMemory _ringBufferMemory;
    private readonly IIpcWaitStrategy _waitStrategy;
    private readonly int _bufferSize;
    private readonly SequencePointer _cursor;
    private readonly SequencePointer* _gatingSequences;
    private volatile int* _gatingSequenceCountPointer;
    private readonly int* _availableBufferPointer;
    private readonly int _indexMask;
    private readonly int _indexShift;
    private SequenceCache _gatingSequenceCache;

    public IpcSequencer(IpcRingBufferMemory ringBufferMemory, IIpcWaitStrategy waitStrategy)
    {
        _bufferSize = ringBufferMemory.BufferSize;
        _ringBufferMemory = ringBufferMemory;
        _waitStrategy = waitStrategy;
        _cursor = ringBufferMemory.Cursor;
        _gatingSequences = ringBufferMemory.GatingSequences;
        _gatingSequenceCountPointer = ringBufferMemory.GatingSequenceCountPointer;
        _availableBufferPointer = ringBufferMemory.AvailabilityBuffer;
        _indexMask = ringBufferMemory.BufferSize - 1;
        _indexShift = DisruptorUtil.Log2(ringBufferMemory.BufferSize);
        _gatingSequenceCache = new SequenceCache(-1);
    }

    internal IpcSequenceBarrier NewBarrier(SequenceWaiterOwner owner, params SequencePointer[] sequencesToTrack)
    {
        var dependentSequences = new IpcDependentSequenceGroup(_cursor, sequencesToTrack);
        var sequenceWaiter = _waitStrategy.NewSequenceWaiter(owner, dependentSequences);

        return new IpcSequenceBarrier(this, sequenceWaiter, dependentSequences);
    }

    public int BufferSize => _bufferSize;

    public long Cursor => _cursor.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAvailableCapacity(int requiredCapacity)
    {
        return HasAvailableCapacity(requiredCapacity, _cursor.Value);
    }

    private bool HasAvailableCapacity(int requiredCapacity, long cursorValue)
    {
        var wrapPoint = (cursorValue + requiredCapacity) - _bufferSize;
        var cachedGatingSequence = _gatingSequenceCache.Value;

        if (wrapPoint > cachedGatingSequence || cachedGatingSequence > cursorValue)
        {
            var minSequence = DisruptorUtil.GetMinimumSequence(_gatingSequences, *_gatingSequenceCountPointer, cursorValue);
            _gatingSequenceCache.SetValue(minSequence);

            if (wrapPoint > minSequence)
            {
                return false;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Next()
    {
        return NextInternal(1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Next(int n)
    {
        if ((uint)(n - 1) >= _bufferSize)
        {
            ThrowHelper.ThrowArgMustBeGreaterThanZeroAndLessThanBufferSize();
        }

        return NextInternal(n);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal long NextInternal(int n)
    {
        var next = _cursor.AddAndGet(n);
        var current = next - n;
        var wrapPoint = next - _bufferSize;
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
        while (wrapPoint > (minSequence = DisruptorUtil.GetMinimumSequence(_gatingSequences, *_gatingSequenceCountPointer, current)))
        {
            spinWait.SpinOnce();
        }

        _gatingSequenceCache.SetValue(minSequence);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryNext(out long sequence)
    {
        return TryNextInternal(1, out sequence);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryNext(int n, out long sequence)
    {
        if (n < 1 || n > _bufferSize)
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
            current = _cursor.Value;
            next = current + n;

            if (!HasAvailableCapacity(n, current))
            {
                sequence = default;
                return false;
            }
        }
        while (!_cursor.CompareAndSet(current, next));

        sequence = next;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetRemainingCapacity()
    {
        var consumed = DisruptorUtil.GetMinimumSequence(_gatingSequences, *_gatingSequenceCountPointer, _cursor.Value);
        var produced = _cursor.Value;
        return BufferSize - (produced - consumed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish(long sequence)
    {
        SetAvailableBufferValue(CalculateIndex(sequence), CalculateAvailabilityFlag(sequence));
    }

    public void Publish(long lo, long hi)
    {
        for (var l = lo; l <= hi; l++)
        {
            SetAvailableBufferValue(CalculateIndex(l), CalculateAvailabilityFlag(l));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetAvailableBufferValue(int index, int flag)
    {
        Volatile.Write(ref _availableBufferPointer[index], flag);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAvailable(long sequence)
    {
        var index = CalculateIndex(sequence);
        var flag = CalculateAvailabilityFlag(sequence);

        return Volatile.Read(ref _availableBufferPointer[index]) == flag;
    }

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

    internal void SetGatingSequences(SequencePointer[] gatingSequences)
    {
        gatingSequences.CopyTo(new Span<SequencePointer>(_gatingSequences, _ringBufferMemory.GatingSequenceCapacity));
        Volatile.Write(ref Unsafe.AsRef<int>(_ringBufferMemory.GatingSequenceCountPointer), gatingSequences.Length);
        // Caching gatingSequenceCount in a field would not be particularly useful because there is already a gating sequence cache.
        // Also, IpcSequencer is used in the IpcPublisher, which needs to read the latest value from the shared memory.
        *_gatingSequenceCountPointer = gatingSequences.Length;
    }

    internal SequencePointer NewSequence()
    {
        return _ringBufferMemory.NewSequence();
    }

    public long GetMinimumSequence()
    {
        return DisruptorUtil.GetMinimumSequence(_gatingSequences, *_gatingSequenceCountPointer, _cursor.Value);
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    private struct SequenceCache
    {
        [FieldOffset(64)]
        private long _value;

        public SequenceCache(long value)
        {
            _value = value;
        }

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
