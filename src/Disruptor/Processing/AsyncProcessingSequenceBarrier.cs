using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Util;
using JetBrains.Annotations;

namespace Disruptor.Processing;

/// <summary>
/// Coordination barrier for asynchronous event processors. <see cref="ProcessingSequenceBarrier{TSequencer,TWaitStrategy}"/>
/// </summary>
/// <typeparam name="TSequencer">the type of the <see cref="ISequencer"/> used.</typeparam>
/// <typeparam name="TWaitStrategy">the type of the <see cref="IAsyncWaitStrategy"/> used.</typeparam>
public class AsyncProcessingSequenceBarrier<TSequencer, TWaitStrategy> : IAsyncSequenceBarrier
    where TWaitStrategy : IAsyncWaitStrategy
    where TSequencer : ISequencer
{
    // ReSharper disable FieldCanBeMadeReadOnly.Local (performance: the runtime type will be a struct)
    private TWaitStrategy _waitStrategy;
    private TSequencer _sequencer;
    // ReSharper restore FieldCanBeMadeReadOnly.Local

    private readonly DependentSequenceGroup _dependentSequences;
    private volatile CancellationTokenSource _cancellationTokenSource;

    [UsedImplicitly]
    public AsyncProcessingSequenceBarrier(TSequencer sequencer, TWaitStrategy waitStrategy, Sequence cursorSequence, ISequence[] dependentSequences)
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

    public async ValueTask<SequenceWaitResult> WaitForAsync(long sequence)
    {
        var cancellationToken = _cancellationTokenSource.Token;
        cancellationToken.ThrowIfCancellationRequested();

        var result = await _waitStrategy.WaitForAsync(sequence, _dependentSequences, cancellationToken).ConfigureAwait(false);

        if (result.UnsafeAvailableSequence < sequence)
            return result;

        return _sequencer.GetHighestPublishedSequence(sequence, result.UnsafeAvailableSequence);
    }

    public long Cursor => _dependentSequences.Value;

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
