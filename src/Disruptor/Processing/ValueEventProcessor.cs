using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.Util;

namespace Disruptor.Processing;

/// <summary>
/// Convenience class for handling the batching semantics of consuming events from a <see cref="ValueRingBuffer{T}"/>
/// and delegating the available events to an <see cref="IValueEventHandler{T}"/>.
/// </summary>
/// <remarks>
/// You should probably not use this type directly but instead implement <see cref="IValueEventHandler{T}"/> and register your handler
/// using <see cref="ValueTypeDisruptor{T}.HandleEventsWith(IValueEventHandler{T}[])"/>.
/// </remarks>
/// <typeparam name="T">the type of event used.</typeparam>
/// <typeparam name="TDataProvider">the type of the <see cref="IValueDataProvider{T}"/> used.</typeparam>
/// <typeparam name="TPublishedSequenceReader">the type of the <see cref="IPublishedSequenceReader"/> used.</typeparam>
/// <typeparam name="TEventHandler">the type of the <see cref="IValueEventHandler{T}"/> used.</typeparam>
/// <typeparam name="TOnBatchStartEvaluator">the type of the <see cref="IOnBatchStartEvaluator"/> used.</typeparam>
/// /// <typeparam name="TBatchSizeLimiter">the type of the <see cref="IBatchSizeLimiter"/> used.</typeparam>
public class ValueEventProcessor<T, TDataProvider, TPublishedSequenceReader, TEventHandler, TOnBatchStartEvaluator, TBatchSizeLimiter> : IValueEventProcessor<T>
    where T : struct

    where TDataProvider : IValueDataProvider<T>
    where TPublishedSequenceReader : IPublishedSequenceReader
    where TEventHandler : IValueEventHandler<T>
    where TOnBatchStartEvaluator : IOnBatchStartEvaluator
    where TBatchSizeLimiter : IBatchSizeLimiter
{
    // ReSharper disable FieldCanBeMadeReadOnly.Local (performance: the runtime type will be a struct)
    private TDataProvider _dataProvider;
    private SequenceBarrier _sequenceBarrier;
    private TPublishedSequenceReader _publishedSequenceReader;
    private TEventHandler _eventHandler;
    private TOnBatchStartEvaluator _onBatchStartEvaluator;
    private TBatchSizeLimiter _batchSizeLimiter;
    // ReSharper restore FieldCanBeMadeReadOnly.Local

    private readonly Sequence _sequence = new();
    private readonly EventProcessorState _state = new EventProcessorState(restartable: true);
    private IValueExceptionHandler<T> _exceptionHandler = new ValueFatalExceptionHandler<T>();

    public ValueEventProcessor(TDataProvider dataProvider, SequenceBarrier sequenceBarrier, TPublishedSequenceReader publishedSequenceReader, TEventHandler eventHandler, TOnBatchStartEvaluator onBatchStartEvaluator, TBatchSizeLimiter batchSizeLimiter)
    {
        _dataProvider = dataProvider;
        _sequenceBarrier = sequenceBarrier;
        _publishedSequenceReader = publishedSequenceReader;
        _eventHandler = eventHandler;
        _onBatchStartEvaluator = onBatchStartEvaluator;
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
    public void SetExceptionHandler(IValueExceptionHandler<T> exceptionHandler)
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

    /// <inheritdoc/>
    /// <remarks>
    /// It is ok to have another thread rerun this method after a halt().
    /// </remarks>
    /// <exception cref="InvalidOperationException">if this object instance is already running in a thread</exception>
    public void Run()
    {
        var runState = _state.Start();
        Run(runState);
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
                var availableSequence = _batchSizeLimiter.ApplyMaxBatchSize(publishedSequence, nextSequence);

                if (_onBatchStartEvaluator.ShouldInvokeOnBatchStart(availableSequence, nextSequence))
                    _eventHandler.OnBatchStart(availableSequence - nextSequence + 1);

                while (nextSequence <= availableSequence)
                {
                    ref T evt = ref _dataProvider[nextSequence];
                    _eventHandler.OnEvent(ref evt, nextSequence, nextSequence == availableSequence);
                    nextSequence++;
                }

                _sequence.SetValue(availableSequence);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                ref T evt = ref _dataProvider[nextSequence];

                _exceptionHandler.HandleEventException(ex, nextSequence, ref evt);
                _sequence.SetValue(nextSequence);
                nextSequence++;
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
