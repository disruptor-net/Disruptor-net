using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Disruptor;

/// <summary>
/// State .
/// Used to store per-caller synchronization primitives.
/// </summary>
public class AsyncWaitState
{
    private readonly ValueTaskSource _valueTaskSource;
    private readonly CancellationToken _cancellationToken;
    private readonly DependentSequenceGroup _dependentSequences;
    private readonly ISequencer? _sequencer;
    private ManualResetValueTaskSourceCore<bool> _valueTaskSourceCore;
    private long _sequence;

    public AsyncWaitState(DependentSequenceGroup dependentSequences, CancellationToken cancellationToken, ISequencer? sequencer = null)
    {
        _valueTaskSource = new(this);
        _cancellationToken = cancellationToken;
        _dependentSequences = dependentSequences;
        _sequencer = sequencer != null && IsSequencerRequired(sequencer, dependentSequences) ? sequencer : null;
        _valueTaskSourceCore = new() { RunContinuationsAsynchronously = true };
    }

    public long CursorValue => _dependentSequences.CursorValue;

    public CancellationToken CancellationToken => _cancellationToken;

    public void ThrowIfCancellationRequested()
    {
        CancellationToken.ThrowIfCancellationRequested();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long AggressiveSpinWaitFor(long sequence)
    {
        return _dependentSequences.AggressiveSpinWaitFor(sequence, _cancellationToken);
    }

    public void Signal()
    {
        _valueTaskSourceCore.SetResult(true);
    }

    public ValueTask<SequenceWaitResult> Wait(long sequence)
    {
        _valueTaskSourceCore.Reset();
        _sequence = sequence;

        return new ValueTask<SequenceWaitResult>(_valueTaskSource, _valueTaskSourceCore.Version);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SequenceWaitResult GetAvailableSequence(long sequence)
    {
        var result = AggressiveSpinWaitFor(sequence);

        if (_sequencer != null && result >= _sequence)
            return _sequencer.GetHighestPublishedSequence(_sequence, result);

        return result;
    }

    private SequenceWaitResult GetResult(short token)
    {
        _valueTaskSourceCore.GetResult(token);

        return GetAvailableSequence(_sequence);
    }

    private ValueTaskSourceStatus GetStatus(short token)
    {
        return _valueTaskSourceCore.GetStatus(token);
    }

    private void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
    {
        _valueTaskSourceCore.OnCompleted(continuation, state, token, flags);
    }

    private static bool IsSequencerRequired(ISequencer sequencer, DependentSequenceGroup dependentSequences)
    {
        var isDependentSequencePublished = ISequenceBarrierOptions.Get(sequencer, dependentSequences) is ISequenceBarrierOptions.IsDependentSequencePublished;
        return !isDependentSequencePublished;
    }

    private class ValueTaskSource : IValueTaskSource<SequenceWaitResult>
    {
        private readonly AsyncWaitState _asyncWaitState;

        public ValueTaskSource(AsyncWaitState asyncWaitState)
        {
            _asyncWaitState = asyncWaitState;
        }

        public SequenceWaitResult GetResult(short token)
        {
            return _asyncWaitState.GetResult(token);
        }

        public ValueTaskSourceStatus GetStatus(short token)
        {
            return _asyncWaitState.GetStatus(token);
        }

        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            _asyncWaitState.OnCompleted(continuation, state, token, flags);
        }
    }
}
