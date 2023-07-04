using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor;

/// <summary>
/// Experimental async enumerable API for the Disruptor.
/// </summary>
/// <remarks>
/// <para>
/// This type generates heap allocations. You can use <see cref="EventPoller{T}"/> if you need to poll the ring buffer without allocations.
/// </para>
/// <para>
/// Consider using <see cref="RingBuffer{T}.NewAsyncEventStream"/> to get an instance of this type.
/// </para>
/// <para>
/// The first enumerator sequence is added as a gating sequence to the ring buffer when the steam is created.
///
/// As a consequence:
/// <list type="bullet">
/// <item>The first iteration will process all events published after the creation of the event stream.</item>
/// <item>The stream will act as a backpressure source right after its creation. Creating a stream and not iterating it will block the ring buffer.</item>
/// </list>
///
/// Subsequent iterations of the stream will start from the last published event.
/// </para>
/// </remarks>
public class AsyncEventStream<T> : IAsyncEnumerable<EventBatch<T>>, IDisposable
    where T : class
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly IDataProvider<T> _dataProvider;
    private readonly IAsyncWaitStrategy _waitStrategy;
    private readonly ISequencer _sequencer;
    private readonly DependentSequenceGroup _dependentSequences;
    private Sequence? _nextEnumeratorSequence;
    private bool _disposed;

    public AsyncEventStream(IDataProvider<T> dataProvider, IAsyncWaitStrategy waitStrategy, ISequencer sequencer, Sequence cursorSequence, params Sequence[] gatingSequences)
    {
        _dataProvider = dataProvider;
        _sequencer = sequencer;
        _dependentSequences = new DependentSequenceGroup(cursorSequence, gatingSequences);
        _waitStrategy = waitStrategy;

        SetNextEnumeratorSequence();
    }

    /// <summary>
    /// Reset the sequence of the next enumerator, so the next iteration will start from the current ring buffer sequence.
    /// This method has no effect on already created enumerators.
    /// </summary>
    public void ResetNextEnumeratorSequence()
    {
        ThrowIfDisposed();
        ClearNextEnumeratorSequence();
        SetNextEnumeratorSequence();
    }

    public IAsyncEnumerator<EventBatch<T>> GetAsyncEnumerator(CancellationToken cancellationToken = new CancellationToken())
    {
        ThrowIfDisposed();

        var sequence = Interlocked.Exchange(ref _nextEnumeratorSequence, null) ?? CreateAndRegisterEnumeratorSequence();

        return new Enumerator(this, sequence, _cancellationTokenSource.Token, cancellationToken);
    }

    /// <summary>
    /// Cancel all active enumerators and remove gating sequences from the ring buffer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();

        ClearNextEnumeratorSequence();
    }

    private Sequence CreateAndRegisterEnumeratorSequence()
    {
        var sequence = new Sequence();
        _sequencer.AddGatingSequences(sequence);

        return sequence;
    }

    private void SetNextEnumeratorSequence()
    {
        _nextEnumeratorSequence = CreateAndRegisterEnumeratorSequence();
    }

    private void ClearNextEnumeratorSequence()
    {
        var sequence = Interlocked.Exchange(ref _nextEnumeratorSequence, null);
        if (sequence != null)
            _sequencer.RemoveGatingSequence(sequence);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            ThrowObjectDisposedException();
        }
    }

    private static void ThrowObjectDisposedException() => throw new ObjectDisposedException(null, "The AsyncEventStream has been disposed.");

    private class Enumerator : IAsyncEnumerator<EventBatch<T>>
    {
        private readonly AsyncEventStream<T> _asyncEventStream;
        private readonly Sequence _sequence;
        private readonly CancellationTokenRegistration _cancellationTokenRegistration;
        private readonly CancellationTokenSource _linkedTokenSource;

        public Enumerator(AsyncEventStream<T> asyncEventStream, Sequence sequence, CancellationToken streamCancellationToken, CancellationToken enumeratorCancellationToken)
        {
            _asyncEventStream = asyncEventStream;
            _sequence = sequence;
            _linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(streamCancellationToken, enumeratorCancellationToken);

            _cancellationTokenRegistration = _linkedTokenSource.Token.Register(x => ((IAsyncWaitStrategy)x!).SignalAllWhenBlocking(), asyncEventStream._waitStrategy);
        }

        public EventBatch<T> Current { get; private set; }

        public async ValueTask DisposeAsync()
        {
            _asyncEventStream._sequencer.RemoveGatingSequence(_sequence);

            await _cancellationTokenRegistration.DisposeAsync().ConfigureAwait(false);

            _linkedTokenSource.Dispose();
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            _sequence.SetValue(_sequence.Value + Current.Length);

            while (true)
            {
                var currentSequence = _sequence.Value;
                var nextSequence = currentSequence + 1;

                _linkedTokenSource.Token.ThrowIfCancellationRequested();

                var waitResult = await _asyncEventStream._waitStrategy.WaitForAsync(nextSequence, _asyncEventStream._dependentSequences, _linkedTokenSource.Token).ConfigureAwait(false);
                if (waitResult.UnsafeAvailableSequence < nextSequence)
                    continue;

                var availableSequence = _asyncEventStream._sequencer.GetHighestPublishedSequence(nextSequence, waitResult.UnsafeAvailableSequence);
                if (availableSequence >= nextSequence)
                {
                    Current = _asyncEventStream._dataProvider.GetBatch(nextSequence, availableSequence);
                    return true;
                }
            }
        }
    }
}
