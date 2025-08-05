using System;
using System.Collections.Generic;

namespace Disruptor.Tests.Support;

public class TestSequencer : ISequencer
{
    private readonly IWaitStrategy _waitStrategy;
    private readonly object _lock = new();
    private readonly Sequence _cursor = new();
    private readonly HashSet<long> _published = new();
    private Sequence[] _gatingSequences = Array.Empty<Sequence>();
    private long _next = Sequence.InitialCursorValue;

    public TestSequencer(int bufferSize, IWaitStrategy waitStrategy)
    {
        _waitStrategy = waitStrategy;
        BufferSize = bufferSize;
    }

    public int BufferSize { get; }

    public bool HasAvailableCapacity(int requiredCapacity)
    {
        lock (_lock)
        {
            var next = _next + requiredCapacity;
            var wrapPoint = next - BufferSize;
            var minimumSequence = DisruptorUtil.GetMinimumSequence(_gatingSequences);

            return wrapPoint <= minimumSequence;
        }
    }

    public long GetRemainingCapacity()
    {
        lock (_lock)
        {
            var nextValue = _next;

            var consumed = DisruptorUtil.GetMinimumSequence(_gatingSequences, nextValue);
            var produced = nextValue;
            return BufferSize - (produced - consumed);
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
                var wrapPoint = next - BufferSize;
                var minimumSequence = DisruptorUtil.GetMinimumSequence(_gatingSequences);
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
            var wrapPoint = next - BufferSize;
            var minimumSequence = DisruptorUtil.GetMinimumSequence(_gatingSequences);
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

            while (_published.Remove(_cursor.Value + 1))
            {
                _cursor.SetValue(_cursor.Value + 1);
            }
        }

        _waitStrategy.SignalAllWhenBlocking();
    }

    public long Cursor => _cursor.Value;

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
            var currentSequence = _cursor.Value;

            return sequence <= currentSequence && sequence > currentSequence - BufferSize;
        }
    }

    public void AddGatingSequences(params Sequence[] gatingSequences)
    {
        lock (_lock)
        {
            SequenceGroups.AddSequences(ref _gatingSequences, this, gatingSequences);
        }
    }

    public bool RemoveGatingSequence(Sequence sequence)
    {
        lock (_lock)
        {
            return SequenceGroups.RemoveSequence(ref _gatingSequences, sequence);
        }
    }

    public SequenceBarrier NewBarrier(SequenceWaiterOwner owner, params Sequence[] sequencesToTrack)
    {
        var dependentSequences = new DependentSequenceGroup(_cursor, sequencesToTrack);
        return new SequenceBarrier(this, _waitStrategy.NewSequenceWaiter(owner, dependentSequences), dependentSequences);
    }

    public AsyncSequenceBarrier NewAsyncBarrier(SequenceWaiterOwner owner, params Sequence[] sequencesToTrack)
    {
        if (_waitStrategy is not IAsyncWaitStrategy asyncWaitStrategy)
            throw new InvalidOperationException($"Unable to create an async event stream: the disruptor must be configured with an async wait strategy (e.g.: {nameof(AsyncWaitStrategy)}");

        var dependentSequences = new DependentSequenceGroup(_cursor, sequencesToTrack);
        return new AsyncSequenceBarrier(this, asyncWaitStrategy.NewAsyncSequenceWaiter(owner, dependentSequences), dependentSequences);
    }

    public long GetMinimumSequence()
    {
        lock (_lock)
        {
            return DisruptorUtil.GetMinimumSequence(_gatingSequences, _cursor.Value);
        }
    }

    public long GetHighestPublishedSequence(long nextSequence, long availableSequence)
    {
        return availableSequence;
    }

    public EventPoller<T> NewPoller<T>(IDataProvider<T> provider, params Sequence[] gatingSequences)
        where T : class
    {
        return EventPoller.Create(provider, this, new Sequence(), _cursor, gatingSequences);
    }

    public ValueEventPoller<T> NewPoller<T>(IValueDataProvider<T> provider, params Sequence[] gatingSequences)
        where T : struct
    {
        return EventPoller.Create(provider, this, new Sequence(), _cursor, gatingSequences);
    }

    public AsyncEventStream<T> NewAsyncEventStream<T>(IDataProvider<T> provider, Sequence[] gatingSequences)
        where T : class
    {
        if (_waitStrategy is not IAsyncWaitStrategy asyncWaitStrategy)
            throw new InvalidOperationException($"Unable to create an async event stream: the disruptor must be configured with an async wait strategy (e.g.: {nameof(AsyncWaitStrategy)}");

        return new AsyncEventStream<T>(provider, asyncWaitStrategy.NewAsyncSequenceWaiter(SequenceWaiterOwner.Unknown, new DependentSequenceGroup(_cursor, gatingSequences)), this);
    }
}
