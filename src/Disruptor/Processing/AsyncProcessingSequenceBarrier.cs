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
internal struct AsyncProcessingSequenceBarrier<TSequencer, TWaitStrategy> : IAsyncSequenceBarrier
    where TWaitStrategy : IAsyncWaitStrategy
    where TSequencer : ISequencer
{
    // ReSharper disable FieldCanBeMadeReadOnly.Local (performance: the runtime type will be a struct)
    private TWaitStrategy _waitStrategy;
    private TSequencer _sequencer;
    // ReSharper restore FieldCanBeMadeReadOnly.Local

    private readonly ISequence _dependentSequence;
    private readonly Sequence _cursorSequence;
    private CancellationTokenSource _cancellationTokenSource;
    private AsyncWaitState _asyncWaitState;

    [UsedImplicitly]
    public AsyncProcessingSequenceBarrier(TSequencer sequencer, TWaitStrategy waitStrategy, Sequence cursorSequence, ISequence[] dependentSequences)
    {
        _sequencer = sequencer;
        _waitStrategy = waitStrategy;
        _cursorSequence = cursorSequence;
        _dependentSequence = SequenceGroups.CreateReadOnlySequence(cursorSequence, dependentSequences);
        _cancellationTokenSource = new CancellationTokenSource();
        _asyncWaitState = new SequencedAsyncWaitState(_cancellationTokenSource.Token, sequencer);
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

    public ValueTask<SequenceWaitResult> WaitForAsync(long sequence)
    {
        var cancellationToken = _cancellationTokenSource.Token;
        cancellationToken.ThrowIfCancellationRequested();

        var cursorSequence = _cursorSequence.Value;
        if (cursorSequence >= sequence)
        {
            return new ValueTask<SequenceWaitResult>(_sequencer.GetHighestPublishedSequence(sequence, cursorSequence));
        }

        return _waitStrategy.WaitForAsync(sequence, _cursorSequence, _dependentSequence, _asyncWaitState);
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
        _asyncWaitState = new SequencedAsyncWaitState(_cancellationTokenSource.Token, _sequencer);
    }

    public void CancelProcessing()
    {
        _cancellationTokenSource.Cancel();
        _waitStrategy.SignalAllWhenBlocking();
    }

    private sealed class SequencedAsyncWaitState : AsyncWaitState
    {
        private readonly TSequencer _sequencer;

        public SequencedAsyncWaitState(CancellationToken cancellationToken, TSequencer sequencer) : base(cancellationToken)
        {
            _sequencer = sequencer;
        }

        public override SequenceWaitResult GetAvailableSequence(long sequence, ISequence dependentSequence)
        {
            var result = base.GetAvailableSequence(sequence, dependentSequence);

            if (result.UnsafeAvailableSequence < sequence)
                return result;

            return _sequencer.GetHighestPublishedSequence(sequence, result.UnsafeAvailableSequence);
        }
    }
}
