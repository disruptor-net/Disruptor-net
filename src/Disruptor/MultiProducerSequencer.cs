using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
    private readonly int _bufferSize;
    private readonly IWaitStrategy _waitStrategy;
    private readonly bool _isBlockingWaitStrategy;
    private readonly Sequence _cursor = new();

    // volatile in the Java version => always use Volatile.Read/Write or Interlocked methods to access this field
    private Sequence[] _gatingSequences = Array.Empty<Sequence>();

    private readonly Sequence _gatingSequenceCache = new();

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

    public MultiProducerSequencer(int bufferSize)
        : this(bufferSize, SequencerFactory.DefaultWaitStrategy())
    {
    }

    public MultiProducerSequencer(int bufferSize, IWaitStrategy waitStrategy)
    {
        if (bufferSize < 1)
        {
            throw new ArgumentException("bufferSize must not be less than 1");
        }
        if (!bufferSize.IsPowerOf2())
        {
            throw new ArgumentException("bufferSize must be a power of 2");
        }

        _bufferSize = bufferSize;
        _waitStrategy = waitStrategy;
        _isBlockingWaitStrategy = waitStrategy.IsBlockingStrategy;
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
    public SequenceBarrier NewBarrier(params Sequence[] sequencesToTrack)
    {
        return new SequenceBarrier(this, _waitStrategy, new DependentSequenceGroup(_cursor, sequencesToTrack));
    }

    /// <inheritdoc/>
    public AsyncSequenceBarrier NewAsyncBarrier(params Sequence[] sequencesToTrack)
    {
        return new AsyncSequenceBarrier(this, _waitStrategy, new DependentSequenceGroup(_cursor, sequencesToTrack));
    }

    /// <inheritdoc/>
    public int BufferSize => _bufferSize;

    /// <inheritdoc/>
    public long Cursor => _cursor.Value;

    /// <inheritdoc/>
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
            var minSequence = DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _gatingSequences), cursorValue);
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
        _cursor.SetValue(sequence);
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
        while (wrapPoint > (minSequence = DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _gatingSequences), current)))
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

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetRemainingCapacity()
    {
        var consumed = DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _gatingSequences), _cursor.Value);
        var produced = _cursor.Value;
        return BufferSize - (produced - consumed);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish(long sequence)
    {
        SetAvailableBufferValue(CalculateIndex(sequence), CalculateAvailabilityFlag(sequence));

        if (_isBlockingWaitStrategy)
        {
            _waitStrategy.SignalAllWhenBlocking();
        }
    }

    /// <inheritdoc/>
    public void Publish(long lo, long hi)
    {
        for (var l = lo; l <= hi; l++)
        {
            SetAvailableBufferValue(CalculateIndex(l), CalculateAvailabilityFlag(l));
        }

        if (_isBlockingWaitStrategy)
        {
            _waitStrategy.SignalAllWhenBlocking();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetAvailableBufferValue(int index, int flag)
    {
#if NETCOREAPP
        _availableBufferPointer[index] = flag;
#else
        _availableBuffer[index] = flag;
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
        SequenceGroups.AddSequences(ref _gatingSequences, this, gatingSequences);
    }

    /// <inheritdoc/>
    public bool RemoveGatingSequence(Sequence sequence)
    {
        return SequenceGroups.RemoveSequence(ref _gatingSequences, sequence);
    }

    /// <inheritdoc/>
    public long GetMinimumSequence()
    {
        return DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _gatingSequences), _cursor.Value);
    }

    /// <inheritdoc/>
    public EventPoller<T> NewPoller<T>(IDataProvider<T> provider, params Sequence[] gatingSequences)
        where T : class
    {
        return EventPoller.Create(provider, this, new Sequence(), _cursor, gatingSequences);
    }

    /// <inheritdoc/>
    public ValueEventPoller<T> NewPoller<T>(IValueDataProvider<T> provider, params Sequence[] gatingSequences)
        where T : struct
    {
        return EventPoller.Create(provider, this, new Sequence(), _cursor, gatingSequences);
    }

    /// <inheritdoc/>
    public AsyncEventStream<T> NewAsyncEventStream<T>(IDataProvider<T> provider, Sequence[] gatingSequences)
        where T : class
    {
        if (_waitStrategy is not IAsyncWaitStrategy asyncWaitStrategy)
            throw new InvalidOperationException($"Unable to create an async event stream: the disruptor must be configured with an async wait strategy (e.g.: {nameof(AsyncWaitStrategy)}");

        return new AsyncEventStream<T>(provider, asyncWaitStrategy, this, _cursor, gatingSequences);
    }

    internal Sequence GetCursorSequence() => _cursor;

    internal IWaitStrategy GetWaitStrategy() => _waitStrategy;
}
