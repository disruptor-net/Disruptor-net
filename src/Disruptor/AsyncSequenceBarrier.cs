using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Util;

namespace Disruptor;

/// <summary>
/// Coordination barrier used by event processors for tracking the ring buffer cursor and the sequences of
/// dependent event processors.
/// </summary>
/// <remarks>
/// <see cref="IDisposable.Dispose"/> should be used to release the sequence barrier, which should only
/// be required for dynamic event processor removal.
/// </remarks>
public sealed class AsyncSequenceBarrier : IDisposable
{
    private readonly ISequencer _sequencer;
    private readonly IAsyncSequenceWaiter _sequenceWaiter;
    private readonly DependentSequenceGroup _dependentSequences;
    private CancellationTokenSource _cancellationTokenSource;

    public AsyncSequenceBarrier(ISequencer sequencer, IAsyncSequenceWaiter sequenceWaiter)
    {
        _sequencer = sequencer;
        _sequenceWaiter = sequenceWaiter;
        _dependentSequences = sequenceWaiter.DependentSequences;
        _cancellationTokenSource = new CancellationTokenSource();
    }

    internal ISequencer Sequencer => _sequencer;

    public DependentSequenceGroup DependentSequences => _dependentSequences;

    public bool IsCancellationRequested => _cancellationTokenSource.IsCancellationRequested;

    public CancellationToken CancellationToken
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _cancellationTokenSource.Token;
    }

    public void ThrowIfCancellationRequested() => _cancellationTokenSource.Token.ThrowIfCancellationRequested();

    /// <summary>
    /// Waits until the requested sequence is available using the <see cref="ISequenceWaiter"/>.
    /// Returns the last available and published sequence, which might be greater than the requested sequence.
    /// </summary>
    /// <remarks>
    /// <p>
    /// The returned value can be timeout (see <see cref="SequenceWaitResult.IsTimeout"/>
    /// </p>
    /// </remarks>
    public async ValueTask<SequenceWaitResult> WaitForPublishedSequenceAsync(long sequence)
    {
        var waitResult = await WaitForAsync(sequence).ConfigureAwait(false);
        return waitResult.IsTimeout ? waitResult : _sequencer.GetHighestPublishedSequence(sequence, waitResult.UnsafeAvailableSequence);
    }

    /// <summary>
    /// Waits until the requested sequence is available using the <see cref="ISequenceWaiter"/>.
    /// Returns the last available sequence, which might be greater than the requested sequence.
    /// </summary>
    /// <remarks>
    /// <p>
    /// The returned value can be timeout (see <see cref="SequenceWaitResult.IsTimeout"/>
    /// </p>
    /// <p>
    /// The return sequence might not be published yet. Use either <see cref="WaitForPublishedSequenceAsync"/>
    /// or a <see cref="IPublishedSequenceReader"/> to ensure the sequence is published.
    /// </p>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | Constants.AggressiveOptimization)]
    public ValueTask<SequenceWaitResult> WaitForAsync(long sequence)
    {
        _cancellationTokenSource.Token.ThrowIfCancellationRequested();

        var availableSequence = _dependentSequences.Value;
        if (availableSequence >= sequence)
            return new ValueTask<SequenceWaitResult>(availableSequence);

        return InvokeWaitStrategy(sequence);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ValueTask<SequenceWaitResult> InvokeWaitStrategy(long sequence)
    {
        return _sequenceWaiter.WaitForAsync(sequence, _cancellationTokenSource.Token);
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
        _sequenceWaiter.Cancel();
    }

    public void Dispose()
    {
        _sequenceWaiter.Dispose();
        _cancellationTokenSource.Dispose();
    }
}
