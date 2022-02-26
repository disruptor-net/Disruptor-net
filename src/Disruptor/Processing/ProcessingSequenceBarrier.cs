using System.Runtime.CompilerServices;
using System.Threading;
using Disruptor.Util;
using JetBrains.Annotations;

namespace Disruptor.Processing;

/// <summary>
/// Coordination barrier for event processors.
/// While there is a single wait strategy for the ring buffer, each <see cref="IEventProcessor"/> has its own <see cref="ISequenceBarrier"/>.
///
/// Used to wait for a cursor sequence (the ring buffer cursor) and optionally a list of dependent sequences (the sequences of the event processors
/// that are configured to run before the current event processor).
/// </summary>
/// <typeparam name="TSequencer">the type of the <see cref="ISequencer"/> used.</typeparam>
/// <typeparam name="TWaitStrategy">the type of the <see cref="IWaitStrategy"/> used.</typeparam>
internal struct ProcessingSequenceBarrier<TSequencer, TWaitStrategy> : ISequenceBarrier
    where TWaitStrategy : IWaitStrategy
    where TSequencer : ISequencer
{
    // ReSharper disable FieldCanBeMadeReadOnly.Local (performance: the runtime type will be a struct)
    private TWaitStrategy _waitStrategy;
    private TSequencer _sequencer;
    // ReSharper restore FieldCanBeMadeReadOnly.Local

    private readonly ISequence _dependentSequence;
    private readonly Sequence _cursorSequence;
    private volatile CancellationTokenSource _cancellationTokenSource;

    [UsedImplicitly]
    public ProcessingSequenceBarrier(TSequencer sequencer, TWaitStrategy waitStrategy, Sequence cursorSequence, ISequence[] dependentSequences)
    {
        _sequencer = sequencer;
        _waitStrategy = waitStrategy;
        _cursorSequence = cursorSequence;
        _dependentSequence = SequenceGroups.CreateReadOnlySequence(cursorSequence, dependentSequences);
        _cancellationTokenSource = new CancellationTokenSource();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | Constants.AggressiveOptimization)]
    public SequenceWaitResult WaitFor(long sequence)
    {
        var cancellationToken = _cancellationTokenSource.Token;
        cancellationToken.ThrowIfCancellationRequested();

        var result = _waitStrategy.WaitFor(sequence, _cursorSequence, _dependentSequence, cancellationToken);

        if (result.UnsafeAvailableSequence < sequence)
            return result;

        return _sequencer.GetHighestPublishedSequence(sequence, result.UnsafeAvailableSequence);
    }

    public long Cursor => _dependentSequence.Value;

    public CancellationToken CancellationToken
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _cancellationTokenSource.Token;
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
