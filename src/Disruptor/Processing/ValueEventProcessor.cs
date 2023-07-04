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
/// using <see cref="ValueDisruptor{T, TR}.HandleEventsWith(IValueEventHandler{T}[])"/>.
/// </remarks>
/// <typeparam name="T">the type of event used.</typeparam>
/// <typeparam name="TDataProvider">the type of the <see cref="IValueDataProvider{T}"/> used.</typeparam>
/// <typeparam name="TSequenceBarrierOptions">the type of the <see cref="ISequenceBarrierOptions"/> used.</typeparam>
/// <typeparam name="TEventHandler">the type of the <see cref="IValueEventHandler{T}"/> used.</typeparam>
/// <typeparam name="TOnBatchStartEvaluator">the type of the <see cref="IOnBatchStartEvaluator"/> used.</typeparam>
/// /// <typeparam name="TBatchSizeLimiter">the type of the <see cref="IBatchSizeLimiter"/> used.</typeparam>
public class ValueEventProcessor<T, TDataProvider, TSequenceBarrierOptions, TEventHandler, TOnBatchStartEvaluator, TBatchSizeLimiter> : IValueEventProcessor<T>
    where T : struct

    where TDataProvider : IValueDataProvider<T>
    where TSequenceBarrierOptions : ISequenceBarrierOptions
    where TEventHandler : IValueEventHandler<T>
    where TOnBatchStartEvaluator : IOnBatchStartEvaluator
    where TBatchSizeLimiter : IBatchSizeLimiter
{
    // ReSharper disable FieldCanBeMadeReadOnly.Local (performance: the runtime type will be a struct)
    private TDataProvider _dataProvider;
    private SequenceBarrier _sequenceBarrier;
    private TEventHandler _eventHandler;
    private TOnBatchStartEvaluator _onBatchStartEvaluator;
    private TBatchSizeLimiter _batchSizeLimiter;
    // ReSharper restore FieldCanBeMadeReadOnly.Local

    private readonly Sequence _sequence = new();
    private readonly ManualResetEventSlim _started = new();
    private IValueExceptionHandler<T> _exceptionHandler = new ValueFatalExceptionHandler<T>();
    private volatile int _running;

    public ValueEventProcessor(TDataProvider dataProvider, SequenceBarrier sequenceBarrier, TEventHandler eventHandler, TOnBatchStartEvaluator onBatchStartEvaluator, TBatchSizeLimiter batchSizeLimiter)
    {
        _dataProvider = dataProvider;
        _sequenceBarrier = sequenceBarrier;
        _eventHandler = eventHandler;
        _onBatchStartEvaluator = onBatchStartEvaluator;
        _batchSizeLimiter = batchSizeLimiter;

        if (eventHandler is IEventProcessorSequenceAware sequenceAware)
            sequenceAware.SetSequenceCallback(_sequence);
    }

    /// <summary>
    /// <see cref="IEventProcessor.Sequence"/>
    /// </summary>
    public Sequence Sequence => _sequence;

    /// <summary>
    /// Signal that this <see cref="IEventProcessor"/> should stop when it has finished consuming at the next clean break.
    /// It will call <see cref="SequenceBarrier.CancelProcessing"/> to notify the thread to check status.
    /// </summary>
    public void Halt()
    {
        _running = ProcessorRunStates.Halted;
        _sequenceBarrier.CancelProcessing();
    }

    /// <summary>
    /// <see cref="IEventProcessor.IsRunning"/>
    /// </summary>
    public bool IsRunning => _running != ProcessorRunStates.Idle;

    /// <summary>
    /// Set a new <see cref="IValueExceptionHandler{T}"/> for handling exceptions propagated out of the <see cref="IValueEventHandler{T}"/>
    /// </summary>
    /// <param name="exceptionHandler">exceptionHandler to replace the existing exceptionHandler.</param>
    public void SetExceptionHandler(IValueExceptionHandler<T> exceptionHandler)
    {
        _exceptionHandler = exceptionHandler ?? throw new ArgumentNullException(nameof(exceptionHandler));
    }

    /// <summary>
    /// Waits before the event processor enters the <see cref="IsRunning"/> state.
    /// </summary>
    /// <param name="timeout">maximum wait duration</param>
    public void WaitUntilStarted(TimeSpan timeout)
    {
        _started.Wait(timeout);
    }

    /// <summary>
    /// <inheritdoc cref="IEventProcessor.Start"/>.
    /// </summary>
    public Task Start(TaskScheduler taskScheduler, TaskCreationOptions taskCreationOptions)
    {
        return taskScheduler.ScheduleAndStart(Run, taskCreationOptions);
    }

    /// <summary>
    /// It is ok to have another thread rerun this method after a halt().
    /// </summary>
    /// <exception cref="InvalidOperationException">if this object instance is already running in a thread</exception>
    public void Run()
    {
#pragma warning disable 420
        var previousRunning = Interlocked.CompareExchange(ref _running, ProcessorRunStates.Running, ProcessorRunStates.Idle);
#pragma warning restore 420

        if (previousRunning == ProcessorRunStates.Running)
        {
            throw new InvalidOperationException("Thread is already running");
        }

        if (previousRunning == ProcessorRunStates.Idle)
        {
            _sequenceBarrier.ResetProcessing();

            NotifyStart();
            try
            {
                if (_running == ProcessorRunStates.Running)
                {
                    ProcessEvents();
                }
            }
            finally
            {
                NotifyShutdown();
                _running = ProcessorRunStates.Idle;
            }
        }
        else
        {
            EarlyExit();
        }
    }

    [MethodImpl(Constants.AggressiveOptimization)]
    private void ProcessEvents()
    {
        var nextSequence = _sequence.Value + 1L;

        while (true)
        {
            try
            {
                var waitResult = _sequenceBarrier.WaitFor<TSequenceBarrierOptions>(nextSequence);
                if (waitResult.IsTimeout)
                {
                    NotifyTimeout();
                    continue;
                }

                var availableSequence = _batchSizeLimiter.ApplyMaxBatchSize(waitResult.UnsafeAvailableSequence, nextSequence);

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
            catch (OperationCanceledException) when (_sequenceBarrier.IsCancellationRequested)
            {
                if (_running != ProcessorRunStates.Running)
                {
                    break;
                }
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

    private void EarlyExit()
    {
        NotifyStart();
        NotifyShutdown();
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
    private void NotifyStart()
    {
        try
        {
            _eventHandler.OnStart();
        }
        catch (Exception e)
        {
            _exceptionHandler.HandleOnStartException(e);
        }

        _started.Set();
    }

    /// <summary>
    /// Notifies the EventHandler immediately prior to this processor shutting down
    /// </summary>
    private void NotifyShutdown()
    {
        try
        {
            _eventHandler.OnShutdown();
        }
        catch (Exception e)
        {
            _exceptionHandler.HandleOnShutdownException(e);
        }

        _started.Reset();
    }
}
