using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Disruptor.Dsl;
using Disruptor.Util;
using static Disruptor.Util.Constants;

namespace Disruptor;

[StructLayout(LayoutKind.Explicit, Size = DefaultPadding * 2 + 48)]
public sealed class SingleProducerSequencer : ISequencer
{
    // padding: DefaultPadding

    [FieldOffset(DefaultPadding)]
    private readonly IWaitStrategy _waitStrategy;

    [FieldOffset(DefaultPadding + 8)]
    private readonly Sequence _cursor = new();

    [FieldOffset(DefaultPadding + 16)]
    // volatile in the Java version => always use Volatile.Read/Write or Interlocked methods to access this field
    private Sequence[] _gatingSequences = Array.Empty<Sequence>();

    [FieldOffset(DefaultPadding + 24)]
    private readonly int _bufferSize;

    [FieldOffset(DefaultPadding + 28)]
    private readonly bool _isBlockingWaitStrategy;

    [FieldOffset(DefaultPadding + 32)]
    private long _nextValue = Sequence.InitialCursorValue;

    [FieldOffset(DefaultPadding + 40)]
    private long _cachedValue = Sequence.InitialCursorValue;

    // padding: DefaultPadding

    public SingleProducerSequencer(int bufferSize)
        : this(bufferSize, SequencerFactory.DefaultWaitStrategy())
    {
    }

    public SingleProducerSequencer(int bufferSize, IWaitStrategy waitStrategy)
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
    }

    /// <inheritdoc cref="ISequencer.NewBarrier"/>
    public SequenceBarrier NewBarrier(SequenceWaiterOwner owner, params Sequence[] sequencesToTrack)
    {
        var dependentSequences = new DependentSequenceGroup(_cursor, sequencesToTrack);
        var sequenceWaiter = _waitStrategy.NewSequenceWaiter(owner, dependentSequences);

        return new SequenceBarrier(this, sequenceWaiter);
    }

    /// <inheritdoc cref="ISequencer.NewAsyncBarrier"/>
    public AsyncSequenceBarrier NewAsyncBarrier(SequenceWaiterOwner owner, params Sequence[] sequencesToTrack)
    {
        if (_waitStrategy is not IAsyncWaitStrategy asyncWaitStrategy)
            throw new InvalidOperationException($"Unable to create an async barrier: the disruptor must be configured with an async wait strategy (e.g.: {nameof(AsyncWaitStrategy)}");

        var dependentSequences = new DependentSequenceGroup(_cursor, sequencesToTrack);
        var sequenceWaiter = asyncWaitStrategy.NewAsyncSequenceWaiter(owner, dependentSequences);

        return new AsyncSequenceBarrier(this, sequenceWaiter);
    }

    /// <inheritdoc/>
    public int BufferSize => _bufferSize;

    /// <inheritdoc/>
    public long Cursor => _cursor.Value;

    /// <inheritdoc/>
    public bool HasAvailableCapacity(int requiredCapacity)
    {
        return HasAvailableCapacity(requiredCapacity, false);
    }

    private bool HasAvailableCapacity(int requiredCapacity, bool doStore)
    {
        long nextValue = _nextValue;

        long wrapPoint = (nextValue + requiredCapacity) - _bufferSize;
        long cachedGatingSequence = _cachedValue;

        if (wrapPoint > cachedGatingSequence || cachedGatingSequence > nextValue)
        {
            if (doStore)
            {
                _cursor.SetValueVolatile(nextValue);
            }

            long minSequence = DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _gatingSequences), nextValue);
            _cachedValue = minSequence;

            if (wrapPoint > minSequence)
            {
                return false;
            }
        }

        return true;
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
        var current = _nextValue;

        var next = current + n;
        var wrapPoint = next - _bufferSize;
        var cachedGatingSequence = _cachedValue;

        if (wrapPoint > cachedGatingSequence || cachedGatingSequence > current)
        {
            NextInternalOnWrapPointReached(current, wrapPoint);
        }

        _nextValue = next;

        return next;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void NextInternalOnWrapPointReached(long current, long wrapPoint)
    {
        _cursor.SetValueVolatile(current);

        var spinWait = default(AggressiveSpinWait);
        long minSequence;
        while (wrapPoint > (minSequence = DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _gatingSequences), current)))
        {
            spinWait.SpinOnce();
        }

        _cachedValue = minSequence;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryNextInternal(int n, out long sequence)
    {
        if (!HasAvailableCapacity(n, true))
        {
            sequence = default(long);
            return false;
        }

        var nextSequence = _nextValue + n;
        _nextValue = nextSequence;

        sequence = nextSequence;
        return true;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetRemainingCapacity()
    {
        var nextValue = _nextValue;

        var consumed = DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _gatingSequences), nextValue);
        var produced = nextValue;
        return BufferSize - (produced - consumed);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Claim(long sequence)
    {
        _nextValue = sequence;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish(long sequence)
    {
        _cursor.SetValue(sequence);

        if (_isBlockingWaitStrategy)
        {
            _waitStrategy.SignalAllWhenBlocking();
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish(long lo, long hi)
    {
        Publish(hi);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAvailable(long sequence)
    {
        var currentSequence = _cursor.Value;
        return sequence <= currentSequence && sequence > currentSequence - _bufferSize;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetHighestPublishedSequence(long nextSequence, long availableSequence)
    {
        return availableSequence;
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

    /// <inheritdoc cref="ISequencer.NewPoller{T}(IDataProvider{T}, Sequence[])"/>.
    public EventPoller<T> NewPoller<T>(IDataProvider<T> provider, params Sequence[] gatingSequences)
        where T : class
    {
        return EventPoller.Create(provider, this, new Sequence(), _cursor, gatingSequences);
    }

    /// <inheritdoc cref="ISequencer.NewPoller{T}(IValueDataProvider{T}, Sequence[])"/>.
    public ValueEventPoller<T> NewPoller<T>(IValueDataProvider<T> provider, params Sequence[] gatingSequences)
        where T : struct
    {
        return EventPoller.Create(provider, this, new Sequence(), _cursor, gatingSequences);
    }

    /// <inheritdoc cref="ISequencer.NewAsyncEventStream{T}(IDataProvider{T}, Sequence[])"/>.
    public AsyncEventStream<T> NewAsyncEventStream<T>(IDataProvider<T> provider, Sequence[] gatingSequences)
        where T : class
    {
        if (_waitStrategy is not IAsyncWaitStrategy asyncWaitStrategy)
            throw new InvalidOperationException($"Unable to create an async event stream: the disruptor must be configured with an async wait strategy (e.g.: {nameof(AsyncWaitStrategy)}");

        var dependentSequences = new DependentSequenceGroup(_cursor, gatingSequences);
        var sequenceWaiter = asyncWaitStrategy.NewAsyncSequenceWaiter(SequenceWaiterOwner.Unknown, dependentSequences);

        return new AsyncEventStream<T>(provider, sequenceWaiter, this);
    }
}
