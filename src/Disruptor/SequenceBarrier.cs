using System;
using System.Runtime.CompilerServices;
using System.Threading;
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
public sealed class SequenceBarrier : IDisposable
{
    private readonly ISequencer _sequencer;
    private readonly ISequenceWaiter _sequenceWaiter;
    private readonly DependentSequenceGroup _dependentSequences;

    public SequenceBarrier(ISequencer sequencer, ISequenceWaiter sequenceWaiter, DependentSequenceGroup dependentSequences)
    {
        _sequencer = sequencer;
        _sequenceWaiter = sequenceWaiter;
        _dependentSequences = dependentSequences;
    }

    internal ISequencer Sequencer => _sequencer;

    public DependentSequenceGroup DependentSequences => _dependentSequences;

    /// <summary>
    /// Waits until the requested sequence is available using the <see cref="ISequenceWaiter"/>.
    /// Returns the last available and published sequence, which might be greater than the requested sequence.
    /// </summary>
    /// <remarks>
    /// <p>
    /// The returned value can be timeout (see <see cref="SequenceWaitResult.IsTimeout"/>
    /// </p>
    /// </remarks>
    public SequenceWaitResult WaitForPublishedSequence(long sequence, CancellationToken cancellationToken = default)
    {
        var waitResult = WaitFor(sequence, cancellationToken);
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
    /// The return sequence might not be published yet. Use either <see cref="WaitForPublishedSequence"/>
    /// or a <see cref="IPublishedSequenceReader"/> to ensure the sequence is published.
    /// </p>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | Constants.AggressiveOptimization)]
    public SequenceWaitResult WaitFor(long sequence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var availableSequence = _dependentSequences.Value;
        if (availableSequence >= sequence)
            return availableSequence;

        return InvokeWaitStrategy(sequence, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private SequenceWaitResult InvokeWaitStrategy(long sequence, CancellationToken cancellationToken)
    {
        return _sequenceWaiter.WaitFor(sequence, cancellationToken);
    }

    public void CancelProcessing()
    {
        _sequenceWaiter.Cancel();
    }

    public void Dispose()
    {
        _sequenceWaiter.Dispose();
    }
}
