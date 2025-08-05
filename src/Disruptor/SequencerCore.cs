using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Disruptor;

internal struct SequencerCore
{
    public readonly IWaitStrategy WaitStrategy;
    public readonly SequencePointer CursorPointer;
    public SequencePointer[] GatingSequencePointers = [];
    public readonly int BufferSize;
    public readonly bool IsBlockingWaitStrategy;
    private readonly RareFields _rareFields = new();

    public SequencerCore(int bufferSize, IWaitStrategy waitStrategy)
    {
        if (bufferSize < 1)
        {
            throw new ArgumentException("bufferSize must not be less than 1");
        }
        if (!bufferSize.IsPowerOf2())
        {
            throw new ArgumentException("bufferSize must be a power of 2");
        }

        BufferSize = bufferSize;
        WaitStrategy = waitStrategy;
        IsBlockingWaitStrategy = waitStrategy.IsBlockingStrategy;
        CursorPointer = _rareFields.Cursor.GetPointer();
    }

    public Sequence Cursor => _rareFields.Cursor;

    public ISequenceWaiter NewSequenceWaiter(SequenceWaiterOwner owner, Sequence[] sequencesToTrack)
    {
        var dependentSequences = new DependentSequenceGroup(_rareFields.Cursor, sequencesToTrack);
        return WaitStrategy.NewSequenceWaiter(owner, dependentSequences);
    }

    public IAsyncSequenceWaiter NewAsyncSequenceWaiter(SequenceWaiterOwner owner, Sequence[] sequencesToTrack)
    {
        if (WaitStrategy is not IAsyncWaitStrategy asyncWaitStrategy)
            throw new InvalidOperationException($"Unable to create an async barrier: the disruptor must be configured with an async wait strategy (e.g.: {nameof(AsyncWaitStrategy)}");

        var dependentSequences = new DependentSequenceGroup(_rareFields.Cursor, sequencesToTrack);
        return asyncWaitStrategy.NewAsyncSequenceWaiter(owner, dependentSequences);
    }

    public void AddGatingSequences(params Sequence[] gatingSequences)
    {
        lock (_rareFields.GatingSequencesLock)
        {
            var newGatingSequences = new Sequence[gatingSequences.Length + _rareFields.GatingSequences.Length];
            _rareFields.GatingSequences.AsSpan().CopyTo(newGatingSequences);
            gatingSequences.AsSpan().CopyTo(newGatingSequences.AsSpan(_rareFields.GatingSequences.Length));

            var newGatingSequencePointers = new SequencePointer[newGatingSequences.Length];
            for (var i = 0; i < newGatingSequences.Length; i++)
            {
                newGatingSequences[i].SetValue(CursorPointer.Value);
                newGatingSequencePointers[i] = newGatingSequences[i].GetPointer();
            }

            _rareFields.GatingSequences = newGatingSequences;
            GatingSequencePointers = newGatingSequencePointers;
        }
    }

    public bool RemoveGatingSequence(Sequence sequence)
    {
        lock (_rareFields.GatingSequencesLock)
        {
            var index = Array.IndexOf(_rareFields.GatingSequences, sequence);
            if (index < 0)
                return false;

            var newGatingSequences = new Sequence[_rareFields.GatingSequences.Length - 1];
            _rareFields.GatingSequences.AsSpan(0, index).CopyTo(newGatingSequences);
            _rareFields.GatingSequences.AsSpan(index + 1).CopyTo(newGatingSequences.AsSpan(index));

            var newGatingSequencePointers = new SequencePointer[newGatingSequences.Length];
            for (var i = 0; i < newGatingSequences.Length; i++)
            {
                newGatingSequencePointers[i] = newGatingSequences[i].GetPointer();
            }

            _rareFields.GatingSequences = newGatingSequences;
            GatingSequencePointers = newGatingSequencePointers;

            return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetMinimumSequence()
    {
        return GetMinimumSequence(CursorPointer.Value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetMinimumSequence(long cursor)
    {
        return DisruptorUtil.GetMinimumSequence(Volatile.Read(ref GatingSequencePointers), cursor);
    }

    private class RareFields
    {
        public readonly Sequence Cursor = new();
        public readonly object GatingSequencesLock = new();
        public Sequence[] GatingSequences = [];
    }
}
