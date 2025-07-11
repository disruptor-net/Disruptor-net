using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.Util;

namespace Disruptor.Processing;

/// <summary>
/// Convenience class for handling the batching semantics of consuming events from a <see cref="RingBuffer{T}"/>
/// and delegating the available events to an <see cref="IBatchEventHandler{T}"/>.
/// </summary>
/// <remarks>
/// You should probably not use this type directly but instead implement <see cref="IBatchEventHandler{T}"/> and register your handler
/// using <see cref="Disruptor{T}.HandleEventsWith(IBatchEventHandler{T}[])"/>.
/// </remarks>
/// <typeparam name="T">the type of event used.</typeparam>
/// <typeparam name="TDataProvider">the type of the <see cref="IDataProvider{T}"/> used.</typeparam>
/// <typeparam name="TPublishedSequenceReader">the type of the <see cref="IPublishedSequenceReader"/> used.</typeparam>
/// <typeparam name="TEventHandler">the type of the <see cref="IBatchEventHandler{T}"/> used.</typeparam>
/// <typeparam name="TBatchSizeLimiter">the type of the <see cref="IBatchSizeLimiter"/> used.</typeparam>
public class AsyncBatchEventProcessor<T, TDataProvider, TPublishedSequenceReader, TEventHandler, TBatchSizeLimiter> : IAsyncEventProcessor<T>
    where T : class
    where TDataProvider : IDataProvider<T>
    where TPublishedSequenceReader : IPublishedSequenceReader
    where TEventHandler : IAsyncBatchEventHandler<T>
    where TBatchSizeLimiter : IBatchSizeLimiter
{
    // ReSharper disable FieldCanBeMadeReadOnly.Local (performance: the runtime type will be a struct)
    private TDataProvider _dataProvider;
    private AsyncSequenceBarrier _sequenceBarrier;
    private TPublishedSequenceReader _publishedSequenceReader;
    private TEventHandler _eventHandler;
    private TBatchSizeLimiter _batchSizeLimiter;
    // ReSharper restore FieldCanBeMadeReadOnly.Local

    private readonly Sequence _sequence = new();
    private readonly EventProcessorState _state = new(restartable: true);
    private IExceptionHandler<T> _exceptionHandler = new FatalExceptionHandler<T>();

    public AsyncBatchEventProcessor(TDataProvider dataProvider, AsyncSequenceBarrier sequenceBarrier, TPublishedSequenceReader publishedSequenceReader, TEventHandler eventHandler, TBatchSizeLimiter batchSizeLimiter)
    {
        _dataProvider = dataProvider;
        _sequenceBarrier = sequenceBarrier;
        _publishedSequenceReader = publishedSequenceReader;
        _eventHandler = eventHandler;
        _batchSizeLimiter = batchSizeLimiter;

        if (eventHandler is IEventProcessorSequenceAware sequenceAware)
            sequenceAware.SetSequenceCallback(_sequence);
    }

    /// <inheritdoc/>
    public Sequence Sequence => _sequence;

    /// <inheritdoc/>
    public Task Halt()
    {
        var runState = _state.Halt();
        if (runState != null)
        {
            _sequenceBarrier.CancelProcessing();
            return runState.ShutdownTask;
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        var runState = _state.Dispose();
        if (runState != null)
        {
            _sequenceBarrier.CancelProcessing();
            runState.ShutdownTask.ContinueWith(_ => _sequenceBarrier.Dispose());
        }
        else
        {
            _sequenceBarrier.Dispose();
        }
    }

    /// <inheritdoc/>
    public bool IsRunning => _state.IsRunning;

    /// <inheritdoc/>
    public void SetExceptionHandler(IExceptionHandler<T> exceptionHandler)
    {
        _exceptionHandler = exceptionHandler ?? throw new ArgumentNullException(nameof(exceptionHandler));
    }

    /// <inheritdoc/>
    public Task Start(TaskScheduler taskScheduler)
    {
        var runState = _state.Start();

        Task.Factory.StartNew(async () => await RunAsync(runState), CancellationToken.None, TaskCreationOptions.None, taskScheduler);

        return runState.StartTask;
    }

    private async Task RunAsync(EventProcessorState.RunState runState)
    {
        NotifyStart(runState);
        try
        {
            await ProcessEvents(runState.CancellationToken).ConfigureAwait(false);
        }
        finally
        {
            NotifyShutdown(runState);
        }
    }

    [MethodImpl(Constants.AggressiveOptimization)]
    private async Task ProcessEvents(CancellationToken cancellationToken)
    {
        var nextSequence = _sequence.Value + 1L;
        var availableSequence = _sequence.Value;

        while (true)
        {
            try
            {
                var waitResult = await _sequenceBarrier.WaitForAsync(nextSequence, cancellationToken).ConfigureAwait(false);
                if (waitResult.IsTimeout)
                {
                    NotifyTimeout();
                    continue;
                }

                var publishedSequence = _publishedSequenceReader.GetHighestPublishedSequence(nextSequence, waitResult.UnsafeAvailableSequence);
                availableSequence = _batchSizeLimiter.ApplyMaxBatchSize(publishedSequence, nextSequence);

                if (availableSequence >= nextSequence)
                {
                    var batch = _dataProvider.GetBatch(nextSequence, availableSequence);
                    await _eventHandler.OnBatch(batch, nextSequence).ConfigureAwait(false);
                    nextSequence += batch.Length;
                }

                _sequence.SetValue(nextSequence - 1);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (availableSequence >= nextSequence)
                {
                    var batch = _dataProvider.GetBatch(nextSequence, availableSequence);
                    _exceptionHandler.HandleEventException(ex, nextSequence, batch);
                    nextSequence += batch.Length;
                    _sequence.SetValue(nextSequence - 1);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void NotifyTimeout()
    {
        try
        {
            _eventHandler.OnTimeout(_sequence.Value);
        }
        catch (Exception ex)
        {
            _exceptionHandler.HandleOnTimeoutException(ex, _sequence.Value);
        }
    }

    /// <summary>
    /// Notifies the EventHandler when this processor is starting up
    /// </summary>
    /// <param name="runState"></param>
    private void NotifyStart(EventProcessorState.RunState runState)
    {
        try
        {
            _eventHandler.OnStart();
        }
        catch (Exception e)
        {
            _exceptionHandler.HandleOnStartException(e);
        }

        runState.OnStarted();
    }

    /// <summary>
    /// Notifies the EventHandler immediately prior to this processor shutting down
    /// </summary>
    /// <param name="runState"></param>
    private void NotifyShutdown(EventProcessorState.RunState runState)
    {
        try
        {
            _eventHandler.OnShutdown();
        }
        catch (Exception e)
        {
            _exceptionHandler.HandleOnShutdownException(e);
        }

        runState.OnShutdown();
    }
}
