using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;
using Disruptor.Util;

namespace Disruptor;

public sealed class AsyncSequenceBarrier
{
    private readonly ISequencer _sequencer;
    private readonly IAsyncWaitStrategy _waitStrategy;
    private readonly DependentSequenceGroup _dependentSequences;
    private CancellationTokenSource _cancellationTokenSource;

    public AsyncSequenceBarrier(ISequencer sequencer, IWaitStrategy waitStrategy, Sequence cursorSequence, ISequence[] dependentSequences)
    {
        if (waitStrategy is not IAsyncWaitStrategy asyncWaitStrategy)
            throw new InvalidOperationException($"The disruptor must be configured with an async wait strategy (e.g.: {nameof(AsyncWaitStrategy)}");

        _sequencer = sequencer;
        _waitStrategy = asyncWaitStrategy;
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
    public ValueTask<SequenceWaitResult> WaitForAsync(long sequence)
    {
        return WaitForAsync<ISequenceBarrierOptions.None>(sequence);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | Constants.AggressiveOptimization)]
    public ValueTask<SequenceWaitResult> WaitForAsync<TSequenceBarrierOptions>(long sequence)
        where TSequenceBarrierOptions : ISequenceBarrierOptions
    {
        _cancellationTokenSource.Token.ThrowIfCancellationRequested();

        var availableSequence = _dependentSequences.Value;
        if (availableSequence >= sequence)
        {
            if (typeof(TSequenceBarrierOptions) == typeof(ISequenceBarrierOptions.IsDependentSequencePublished))
            {
                return new ValueTask<SequenceWaitResult>(availableSequence);
            }

            return new ValueTask<SequenceWaitResult>(_sequencer.GetHighestPublishedSequence(sequence, availableSequence));
        }

        if (typeof(TSequenceBarrierOptions) == typeof(ISequenceBarrierOptions.IsDependentSequencePublished))
        {
            return InvokeWaitStrategy(sequence);
        }

        return InvokeWaitStrategyAndWaitForPublishedSequence(sequence);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ValueTask<SequenceWaitResult> InvokeWaitStrategy(long sequence)
    {
        return _waitStrategy.WaitForAsync(sequence, _dependentSequences, _cancellationTokenSource.Token);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async ValueTask<SequenceWaitResult> InvokeWaitStrategyAndWaitForPublishedSequence(long sequence)
    {
        var waitResult = await _waitStrategy.WaitForAsync(sequence, _dependentSequences, _cancellationTokenSource.Token).ConfigureAwait(false);

        return waitResult.UnsafeAvailableSequence >= sequence ? _sequencer.GetHighestPublishedSequence(sequence, waitResult.UnsafeAvailableSequence) : waitResult;
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
