using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Disruptor.Dsl;
using Disruptor.Util;

namespace Disruptor;

public sealed class SingleProducerSequencer : ISequencer
{
    private SequencerCore _sequencerCore;
    private PaddedSequences _sequences;

    public SingleProducerSequencer(int bufferSize)
        : this(bufferSize, SequencerFactory.DefaultWaitStrategy())
    {
    }

    public SingleProducerSequencer(int bufferSize, IWaitStrategy waitStrategy)
    {
        _sequencerCore = new SequencerCore(bufferSize, waitStrategy);
        _sequences = new PaddedSequences
        {
            NextValue = Sequence.InitialCursorValue,
            GatingSequenceCache = Sequence.InitialCursorValue,
        };
    }

    /// <inheritdoc cref="ISequencer.NewBarrier"/>
    public SequenceBarrier NewBarrier(SequenceWaiterOwner owner, params Sequence[] sequencesToTrack)
    {
        var sequenceWaiter = _sequencerCore.NewSequenceWaiter(owner, sequencesToTrack);

        return new SequenceBarrier(this, sequenceWaiter);
    }

    /// <inheritdoc cref="ISequencer.NewAsyncBarrier"/>
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
    public bool HasAvailableCapacity(int requiredCapacity)
    {
        return HasAvailableCapacity(requiredCapacity, false);
    }

    private bool HasAvailableCapacity(int requiredCapacity, bool doStore)
    {
        long nextValue = _sequences.NextValue;

        long wrapPoint = (nextValue + requiredCapacity) - _sequencerCore.BufferSize;
        long cachedGatingSequence = _sequences.GatingSequenceCache;

        if (wrapPoint > cachedGatingSequence || cachedGatingSequence > nextValue)
        {
            if (doStore)
            {
                _sequencerCore.CursorPointer.SetValueVolatile(nextValue);
            }

            long minSequence = DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _sequencerCore.GatingSequencePointers), nextValue);
            _sequences.GatingSequenceCache = minSequence;

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
        if ((uint)(n - 1) >= _sequencerCore.BufferSize)
        {
            ThrowHelper.ThrowArgMustBeGreaterThanZeroAndLessThanBufferSize();
        }

        return NextInternal(n);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal long NextInternal(int n)
    {
        var current = _sequences.NextValue;

        var next = current + n;
        var wrapPoint = next - _sequencerCore.BufferSize;
        var cachedGatingSequence = _sequences.GatingSequenceCache;

        if (wrapPoint > cachedGatingSequence || cachedGatingSequence > current)
        {
            NextInternalOnWrapPointReached(current, wrapPoint);
        }

        _sequences.NextValue = next;

        return next;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void NextInternalOnWrapPointReached(long current, long wrapPoint)
    {
        _sequencerCore.CursorPointer.SetValueVolatile(current);

        var spinWait = default(AggressiveSpinWait);
        long minSequence;
        while (wrapPoint > (minSequence = DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _sequencerCore.GatingSequencePointers), current)))
        {
            spinWait.SpinOnce();
        }

        _sequences.GatingSequenceCache = minSequence;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryNextInternal(int n, out long sequence)
    {
        if (!HasAvailableCapacity(n, true))
        {
            sequence = default(long);
            return false;
        }

        var nextSequence = _sequences.NextValue + n;
        _sequences.NextValue = nextSequence;

        sequence = nextSequence;
        return true;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetRemainingCapacity()
    {
        var nextValue = _sequences.NextValue;

        var consumed = DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _sequencerCore.GatingSequencePointers), nextValue);
        var produced = nextValue;
        return BufferSize - (produced - consumed);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Claim(long sequence)
    {
        _sequences.NextValue = sequence;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish(long sequence)
    {
        _sequencerCore.CursorPointer.SetValue(sequence);

        if (_sequencerCore.IsBlockingWaitStrategy)
        {
            _sequencerCore.WaitStrategy.SignalAllWhenBlocking();
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
        var currentSequence = _sequencerCore.CursorPointer.Value;
        return sequence <= currentSequence && sequence > currentSequence - _sequencerCore.BufferSize;
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

    /// <inheritdoc cref="ISequencer.NewPoller{T}(IDataProvider{T}, Sequence[])"/>.
    public EventPoller<T> NewPoller<T>(IDataProvider<T> provider, params Sequence[] gatingSequences)
        where T : class
    {
        return EventPoller.Create(provider, this, new Sequence(), _sequencerCore.Cursor, gatingSequences);
    }

    /// <inheritdoc cref="ISequencer.NewPoller{T}(IValueDataProvider{T}, Sequence[])"/>.
    public ValueEventPoller<T> NewPoller<T>(IValueDataProvider<T> provider, params Sequence[] gatingSequences)
        where T : struct
    {
        return EventPoller.Create(provider, this, new Sequence(), _sequencerCore.Cursor, gatingSequences);
    }

    /// <inheritdoc cref="ISequencer.NewAsyncEventStream{T}(IDataProvider{T}, Sequence[])"/>.
    public AsyncEventStream<T> NewAsyncEventStream<T>(IDataProvider<T> provider, Sequence[] gatingSequences)
        where T : class
    {
        var sequenceWaiter = _sequencerCore.NewAsyncSequenceWaiter(SequenceWaiterOwner.Unknown, gatingSequences);

        return new AsyncEventStream<T>(provider, sequenceWaiter, this);
    }

    [StructLayout(LayoutKind.Explicit, Size = 128 + 8)]
    private struct PaddedSequences
    {
        [FieldOffset(64)]
        public long NextValue;
        [FieldOffset(64 + 8)]
        public long GatingSequenceCache;
    }
}
