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
/// <typeparam name="TPublishedSequenceReader">the type of the <see cref="IPublishedSequenceReader"/> used.</typeparam>
/// <typeparam name="TEventHandler">the type of the <see cref="IBatchEventHandler{T}"/> used.</typeparam>
/// <typeparam name="TBatchSizeLimiter">the type of the <see cref="IBatchSizeLimiter"/> used.</typeparam>
public class BatchEventProcessor<T, TPublishedSequenceReader, TEventHandler, TBatchSizeLimiter> : IEventProcessor<T>
    where T : class
    where TPublishedSequenceReader : IPublishedSequenceReader
    where TEventHandler : IBatchEventHandler<T>
    where TBatchSizeLimiter : IBatchSizeLimiter
{
    // ReSharper disable FieldCanBeMadeReadOnly.Local (performance: the runtime type will be a struct)
    private RingBuffer<T> _dataProvider;
    private SequenceBarrier _sequenceBarrier;
    private TPublishedSequenceReader _publishedSequenceReader;
    private TEventHandler _eventHandler;
    private TBatchSizeLimiter _batchSizeLimiter;
    // ReSharper restore FieldCanBeMadeReadOnly.Local

    private readonly Sequence _sequence = new();
    private readonly EventProcessorState _state;
    private IExceptionHandler<T> _exceptionHandler = new FatalExceptionHandler<T>();

    public BatchEventProcessor(RingBuffer<T> dataProvider, SequenceBarrier sequenceBarrier, TPublishedSequenceReader publishedSequenceReader, TEventHandler eventHandler, TBatchSizeLimiter batchSizeLimiter)
    {
        _dataProvider = dataProvider;
        _sequenceBarrier = sequenceBarrier;
        _publishedSequenceReader = publishedSequenceReader;
        _eventHandler = eventHandler;
        _batchSizeLimiter = batchSizeLimiter;
        _state = new EventProcessorState(sequenceBarrier, restartable: true);

        if (eventHandler is IEventProcessorSequenceAware sequenceAware)
            sequenceAware.SetSequenceCallback(_sequence);
    }

    /// <inheritdoc/>
    public Sequence Sequence => _sequence;

    /// <inheritdoc/>
    public Task Halt()
    {
        return _state.Halt();
    }

    public void Dispose()
    {
        _state.Dispose();
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
        taskScheduler.StartLongRunningTask(() => Run(runState));

        return runState.StartTask;
    }

    private void Run(EventProcessorState.RunState runState)
    {
        NotifyStart(runState);
        try
        {
            ProcessEvents(runState.CancellationToken);
        }
        finally
        {
            NotifyShutdown(runState);
        }
    }

    [MethodImpl(Constants.AggressiveOptimization)]
    private void ProcessEvents(CancellationToken cancellationToken)
    {
        var nextSequence = _sequence.Value + 1L;
        var availableSequence = _sequence.Value;

        while (true)
        {
            try
            {
                var waitResult = _sequenceBarrier.WaitFor(nextSequence, cancellationToken);
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
                    _eventHandler.OnBatch(batch, nextSequence);
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
                }

                _sequence.SetValue(nextSequence - 1);
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
