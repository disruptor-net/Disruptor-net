﻿using System;
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

    /// <inheritdoc />
    public SequenceBarrier NewBarrier(int eventHandlerGroupPosition, params Sequence[] sequencesToTrack)
    {
        return new SequenceBarrier(this, _waitStrategy, _cursor, eventHandlerGroupPosition, sequencesToTrack);
    }

    /// <inheritdoc />
    public AsyncSequenceBarrier NewAsyncBarrier(int eventHandlerGroupPosition, params Sequence[] sequencesToTrack)
    {
        return new AsyncSequenceBarrier(this, _waitStrategy, _cursor, eventHandlerGroupPosition, sequencesToTrack);
    }

    /// <inheritdoc cref="ISequenced.BufferSize"/>.
    public int BufferSize => _bufferSize;

    /// <inheritdoc cref="ICursored.Cursor"/>.
    public long Cursor => _cursor.Value;

    /// <inheritdoc cref="ISequenced.HasAvailableCapacity"/>.
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

    /// <inheritdoc cref="ISequenced.Next()"/>.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Next()
    {
        return NextInternal(1);
    }

    /// <inheritdoc cref="ISequenced.Next(int)"/>.
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

    /// <inheritdoc cref="ISequenced.TryNext(out long)"/>.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryNext(out long sequence)
    {
        return TryNextInternal(1, out sequence);
    }

    /// <inheritdoc cref="ISequenced.TryNext(int, out long)"/>.
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

    /// <inheritdoc cref="ISequenced.GetRemainingCapacity"/>.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetRemainingCapacity()
    {
        var nextValue = _nextValue;

        var consumed = DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _gatingSequences), nextValue);
        var produced = nextValue;
        return BufferSize - (produced - consumed);
    }

    /// <inheritdoc cref="ISequencer.Claim"/>.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Claim(long sequence)
    {
        _nextValue = sequence;
    }

    /// <inheritdoc cref="ISequenced.Publish(long)"/>.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish(long sequence)
    {
        _cursor.SetValue(sequence);

        if (_isBlockingWaitStrategy)
        {
            _waitStrategy.SignalAllWhenBlocking();
        }
    }

    /// <inheritdoc cref="ISequenced.Publish(long, long)"/>.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish(long lo, long hi)
    {
        Publish(hi);
    }

    /// <inheritdoc cref="ISequencer.IsAvailable"/>.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAvailable(long sequence)
    {
        var currentSequence = _cursor.Value;
        return sequence <= currentSequence && sequence > currentSequence - _bufferSize;
    }

    /// <inheritdoc cref="ISequencer.GetHighestPublishedSequence"/>.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetHighestPublishedSequence(long nextSequence, long availableSequence)
    {
        return availableSequence;
    }

    /// <inheritdoc cref="ISequencer.AddGatingSequences"/>.
    public void AddGatingSequences(params Sequence[] gatingSequences)
    {
        SequenceGroups.AddSequences(ref _gatingSequences, this, gatingSequences);
    }

    /// <inheritdoc cref="ISequencer.RemoveGatingSequence"/>.
    public bool RemoveGatingSequence(Sequence sequence)
    {
        return SequenceGroups.RemoveSequence(ref _gatingSequences, sequence);
    }

    /// <inheritdoc cref="ISequencer.GetMinimumSequence"/>.
    public long GetMinimumSequence()
    {
        return DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _gatingSequences), _cursor.Value);
    }

    /// <inheritdoc />.
    public EventPoller<T> NewPoller<T>(IDataProvider<T> provider, int eventHandlerGroupPosition, params Sequence[] gatingSequences)
        where T : class
    {
        var dependentSequences = new DependentSequenceGroup(_cursor, eventHandlerGroupPosition, gatingSequences);

        return EventPoller.Create(provider, this, new Sequence(), dependentSequences);
    }

    /// <inheritdoc />.
    public ValueEventPoller<T> NewPoller<T>(IValueDataProvider<T> provider, int eventHandlerGroupPosition, params Sequence[] gatingSequences)
        where T : struct
    {
        var dependentSequences = new DependentSequenceGroup(_cursor, eventHandlerGroupPosition, gatingSequences);

        return EventPoller.Create(provider, this, new Sequence(), dependentSequences);
    }

    /// <inheritdoc />.
    public AsyncEventStream<T> NewAsyncEventStream<T>(IDataProvider<T> provider, int eventHandlerGroupPosition, Sequence[] gatingSequences)
        where T : class
    {
        if (_waitStrategy is not IAsyncWaitStrategy asyncWaitStrategy)
            throw new InvalidOperationException($"Unable to create an async event stream: the disruptor must be configured with an async wait strategy (e.g.: {nameof(AsyncWaitStrategy)}");

        var dependentSequences = new DependentSequenceGroup(_cursor, eventHandlerGroupPosition, gatingSequences);

        return new AsyncEventStream<T>(provider, asyncWaitStrategy, this, dependentSequences);
    }
}
