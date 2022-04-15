using System.Runtime.CompilerServices;
using System.Threading;
using Disruptor.Util;
using JetBrains.Annotations;

namespace Disruptor.Processing;

/// <summary>
/// <see cref="ISequenceBarrier"/> handed out for gating <see cref="IEventProcessor"/> on a cursor sequence and optional dependent <see cref="IEventProcessor"/>s,
///  using the given WaitStrategy.
/// </summary>
/// <typeparam name="TSequencer">the type of the <see cref="ISequencer"/> used.</typeparam>
/// <typeparam name="TWaitStrategy">the type of the <see cref="IWaitStrategy"/> used.</typeparam>
public class ProcessingSequenceBarrier<TSequencer, TWaitStrategy> : ISequenceBarrier
    where TWaitStrategy : IWaitStrategy
    where TSequencer : ISequencer
{
    // ReSharper disable FieldCanBeMadeReadOnly.Local (performance: the runtime type will be a struct)
    private TWaitStrategy _waitStrategy;
    private TSequencer _sequencer;
    // ReSharper restore FieldCanBeMadeReadOnly.Local

    private readonly DependentSequenceGroup _dependentSequences;
    private volatile CancellationTokenSource _cancellationTokenSource;

    [UsedImplicitly]
    public ProcessingSequenceBarrier(TSequencer sequencer, TWaitStrategy waitStrategy, Sequence cursorSequence, ISequence[] dependentSequences)
    {
        _sequencer = sequencer;
        _waitStrategy = waitStrategy;
        _dependentSequences = new DependentSequenceGroup(cursorSequence, dependentSequences);
        _cancellationTokenSource = new CancellationTokenSource();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | Constants.AggressiveOptimization)]
    public SequenceWaitResult WaitFor(long sequence)
    {
        var cancellationToken = _cancellationTokenSource.Token;
        cancellationToken.ThrowIfCancellationRequested();

        var result = _waitStrategy.WaitFor(sequence, _dependentSequences, cancellationToken);

        if (result.UnsafeAvailableSequence < sequence)
            return result;

        return _sequencer.GetHighestPublishedSequence(sequence, result.UnsafeAvailableSequence);
    }

    public DependentSequenceGroup DependentSequences => _dependentSequences;

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
