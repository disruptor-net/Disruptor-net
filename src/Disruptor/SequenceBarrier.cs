using System.Runtime.CompilerServices;
using System.Threading;
using Disruptor.Util;

namespace Disruptor;

/// <summary>
/// Coordination barrier used by event processors for tracking the ring buffer cursor and the sequences of
/// dependent event processors.
/// </summary>
public sealed class SequenceBarrier
{
    private readonly ISequencer _sequencer;
    private readonly IWaitStrategy _waitStrategy;
    private readonly DependentSequenceGroup _dependentSequences;
    private CancellationTokenSource _cancellationTokenSource;

    public SequenceBarrier(ISequencer sequencer, IWaitStrategy waitStrategy, Sequence cursorSequence, ISequence[] dependentSequences)
    {
        _sequencer = sequencer;
        _waitStrategy = waitStrategy;
        _dependentSequences = new DependentSequenceGroup(cursorSequence, dependentSequences);
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public DependentSequenceGroup DependentSequences => _dependentSequences;

    public bool IsCancellationRequested => _cancellationTokenSource.IsCancellationRequested;

    public CancellationToken CancellationToken
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _cancellationTokenSource.Token;
    }

    public void ThrowIfCancellationRequested() => _cancellationTokenSource.Token.ThrowIfCancellationRequested();

    public ISequenceBarrierOptions GetSequencerOptions()
    {
        return ISequenceBarrierOptions.Get(_sequencer, _dependentSequences);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | Constants.AggressiveOptimization)]
    public SequenceWaitResult WaitFor(long sequence)
    {
        return WaitFor<ISequenceBarrierOptions.None>(sequence);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | Constants.AggressiveOptimization)]
    public SequenceWaitResult WaitFor<TSequenceBarrierOptions>(long sequence)
        where TSequenceBarrierOptions : ISequenceBarrierOptions
    {
        _cancellationTokenSource.Token.ThrowIfCancellationRequested();

        var availableSequence = _dependentSequences.Value;
        if (availableSequence >= sequence)
        {
            if (typeof(TSequenceBarrierOptions) == typeof(ISequenceBarrierOptions.IsDependentSequencePublished))
                return availableSequence;

            return _sequencer.GetHighestPublishedSequence(sequence, availableSequence);
        }

        if (typeof(TSequenceBarrierOptions) == typeof(ISequenceBarrierOptions.IsDependentSequencePublished))
        {
            return InvokeWaitStrategy(sequence);
        }

        return InvokeWaitStrategyAndWaitForPublishedSequence(sequence);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private SequenceWaitResult InvokeWaitStrategy(long sequence)
    {
        return _waitStrategy.WaitFor(sequence, _dependentSequences, _cancellationTokenSource.Token);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private SequenceWaitResult InvokeWaitStrategyAndWaitForPublishedSequence(long sequence)
    {
        var waitResult = _waitStrategy.WaitFor(sequence, _dependentSequences, _cancellationTokenSource.Token);

        if (waitResult.UnsafeAvailableSequence >= sequence)
        {
            return _sequencer.GetHighestPublishedSequence(sequence, waitResult.UnsafeAvailableSequence);
        }

        return waitResult;
    }

    public void ResetProcessing()
    {
        // Not disposing the previous value should be fine because the CancellationTokenSource instance
        // has no finalizer and no unmanaged resources to release.

        _cancellationTokenSource = new CancellationTokenSource();
    }

    public void CancelProcessing()
    {
        _cancellationTokenSource.Cancel();
        _waitStrategy.SignalAllWhenBlocking();
    }
}
