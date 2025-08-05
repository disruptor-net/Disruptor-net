using System.Collections.Generic;

namespace Disruptor.Tests.Support;

public class TestSequencer : ISequencer
{
    private SequencerCore _sequencerCore;
    private readonly object _lock = new();
    private readonly HashSet<long> _published = new();
    private long _next = Sequence.InitialCursorValue;

    public TestSequencer(int bufferSize, IWaitStrategy waitStrategy)
    {
        _sequencerCore = new SequencerCore(bufferSize, waitStrategy);
    }

    public int BufferSize => _sequencerCore.BufferSize;
    public long Cursor => _sequencerCore.CursorPointer.Value;

    public bool HasAvailableCapacity(int requiredCapacity)
    {
        lock (_lock)
        {
            var next = _next + requiredCapacity;
            var wrapPoint = next - _sequencerCore.BufferSize;
            var minimumSequence = _sequencerCore.GetMinimumSequence(next);

            return wrapPoint <= minimumSequence;
        }
    }

    public long GetRemainingCapacity()
    {
        lock (_lock)
        {
            var nextValue = _next;

            var consumed = _sequencerCore.GetMinimumSequence(nextValue);
            var produced = nextValue;
            return _sequencerCore.BufferSize - (produced - consumed);
        }
    }

    public long Next()
    {
        return Next(1);
    }

    public long Next(int n)
    {
        var spinner = new AggressiveSpinWait();
        while (true)
        {
            lock (_lock)
            {
                var next = _next + n;
                var wrapPoint = next - _sequencerCore.BufferSize;
                var minimumSequence = _sequencerCore.GetMinimumSequence();
                if (wrapPoint <= minimumSequence)
                {
                    _next = next;
                    return next;
                }
            }

            spinner.SpinOnce();
        }
    }

    public bool TryNext(out long sequence)
    {
        return TryNext(1, out sequence);
    }

    public bool TryNext(int n, out long sequence)
    {
        lock (_lock)
        {
            var next = _next + n;
            var wrapPoint = next - _sequencerCore.BufferSize;
            var minimumSequence = _sequencerCore.GetMinimumSequence();
            if (wrapPoint <= minimumSequence)
            {
                _next = next;
                sequence = next;
                return true;
            }

            sequence = default;
            return false;
        }
    }

    public void Publish(long sequence)
    {
        Publish(sequence, sequence);
    }

    public void Publish(long lo, long hi)
    {
        lock (_lock)
        {
            for (var s = lo; s <= hi; s++)
            {
                _published.Add(s);
            }

            while (_published.Remove(_sequencerCore.CursorPointer.Value + 1))
            {
                _sequencerCore.CursorPointer.SetValue(_sequencerCore.CursorPointer.Value + 1);
            }
        }

        _sequencerCore.WaitStrategy.SignalAllWhenBlocking();
    }

    public void Claim(long sequence)
    {
        lock (_lock)
        {
            _next = sequence;
        }
    }

    public bool IsAvailable(long sequence)
    {
        lock (_lock)
        {
            var currentSequence = _sequencerCore.CursorPointer.Value;

            return sequence <= currentSequence && sequence > currentSequence - _sequencerCore.BufferSize;
        }
    }

    public void AddGatingSequences(params Sequence[] gatingSequences)
    {
        lock (_lock)
        {
            _sequencerCore.AddGatingSequences(gatingSequences);
        }
    }

    public bool RemoveGatingSequence(Sequence sequence)
    {
        lock (_lock)
        {
            return _sequencerCore.RemoveGatingSequence(sequence);
        }
    }

    public SequenceBarrier NewBarrier(SequenceWaiterOwner owner, params Sequence[] sequencesToTrack)
    {
        return new SequenceBarrier(this, _sequencerCore.NewSequenceWaiter(owner, sequencesToTrack));
    }

    public AsyncSequenceBarrier NewAsyncBarrier(SequenceWaiterOwner owner, params Sequence[] sequencesToTrack)
    {
        return new AsyncSequenceBarrier(this, _sequencerCore.NewAsyncSequenceWaiter(owner, sequencesToTrack));
    }

    public long GetMinimumSequence()
    {
        lock (_lock)
        {
            return _sequencerCore.GetMinimumSequence();
        }
    }

    public long GetHighestPublishedSequence(long nextSequence, long availableSequence)
    {
        return availableSequence;
    }

    public EventPoller<T> NewPoller<T>(IDataProvider<T> provider, params Sequence[] gatingSequences)
        where T : class
    {
        return EventPoller.Create(provider, this, new Sequence(), _sequencerCore.Cursor, gatingSequences);
    }

    public ValueEventPoller<T> NewPoller<T>(IValueDataProvider<T> provider, params Sequence[] gatingSequences)
        where T : struct
    {
        return EventPoller.Create(provider, this, new Sequence(), _sequencerCore.Cursor, gatingSequences);
    }

    public AsyncEventStream<T> NewAsyncEventStream<T>(IDataProvider<T> provider, Sequence[] gatingSequences)
        where T : class
    {
        return new AsyncEventStream<T>(provider, _sequencerCore.NewAsyncSequenceWaiter(SequenceWaiterOwner.Unknown, gatingSequences), this);
    }
}
