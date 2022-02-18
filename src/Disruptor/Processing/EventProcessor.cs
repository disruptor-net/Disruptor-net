﻿using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.Util;

namespace Disruptor.Processing;

/// <summary>
/// Convenience class for handling the batching semantics of consuming events from a <see cref="RingBuffer{T}"/>
/// and delegating the available events to an <see cref="IEventHandler{T}"/>.
/// </summary>
/// <remarks>
/// You should probably not use this type directly but instead implement <see cref="IEventHandler{T}"/> and register your handler
/// using <see cref="Disruptor{T}.HandleEventsWith(IEventHandler{T}[])"/>.
/// </remarks>
/// <typeparam name="T">the type of event used.</typeparam>
/// <typeparam name="TDataProvider">the type of the <see cref="IDataProvider{T}"/> used.</typeparam>
/// <typeparam name="TSequenceBarrier">the type of the <see cref="ISequenceBarrier"/> used.</typeparam>
/// <typeparam name="TEventHandler">the type of the <see cref="IEventHandler{T}"/> used.</typeparam>
/// <typeparam name="TOnBatchStartEvaluator">the type of the <see cref="IOnBatchStartEvaluator"/> used.</typeparam>
public class EventProcessor<T, TDataProvider, TSequenceBarrier, TEventHandler, TOnBatchStartEvaluator> : IEventProcessor<T>
    where T : class
    where TDataProvider : IDataProvider<T>
    where TSequenceBarrier : ISequenceBarrier
    where TEventHandler : IEventHandler<T>
    where TOnBatchStartEvaluator : IOnBatchStartEvaluator
{
    // ReSharper disable FieldCanBeMadeReadOnly.Local (performance: the runtime type will be a struct)
    private TDataProvider _dataProvider;
    private TSequenceBarrier _sequenceBarrier;
    private TEventHandler _eventHandler;
    private TOnBatchStartEvaluator _onBatchStartEvaluator;
    // ReSharper restore FieldCanBeMadeReadOnly.Local

    private readonly Sequence _sequence = new();
    private readonly ManualResetEventSlim _started = new();
    private IExceptionHandler<T> _exceptionHandler = new FatalExceptionHandler<T>();
    private volatile int _runState = ProcessorRunStates.Idle;

    public EventProcessor(TDataProvider dataProvider, TSequenceBarrier sequenceBarrier, TEventHandler eventHandler, TOnBatchStartEvaluator onBatchStartEvaluator)
    {
        _dataProvider = dataProvider;
        _sequenceBarrier = sequenceBarrier;
        _eventHandler = eventHandler;
        _onBatchStartEvaluator = onBatchStartEvaluator;

        if (eventHandler is IEventProcessorSequenceAware sequenceAware)
            sequenceAware.SetSequenceCallback(_sequence);
    }

    /// <summary>
    /// <see cref="IEventProcessor.Sequence"/>
    /// </summary>
    public ISequence Sequence => _sequence;

    /// <summary>
    /// Signal that this <see cref="IEventProcessor"/> should stop when it has finished consuming at the next clean break.
    /// It will call <see cref="ISequenceBarrier.CancelProcessing"/> to notify the thread to check status.
    /// </summary>
    public void Halt()
    {
        _runState = ProcessorRunStates.Halted;
        _sequenceBarrier.CancelProcessing();
    }

    /// <summary>
    /// <see cref="IEventProcessor.IsRunning"/>
    /// </summary>
    public bool IsRunning => _runState != ProcessorRunStates.Idle;

    /// <summary>
    /// Set a new <see cref="IExceptionHandler{T}"/> for handling exceptions propagated out of the <see cref="IEventHandler{T}"/>
    /// </summary>
    /// <param name="exceptionHandler">exceptionHandler to replace the existing exceptionHandler.</param>
    public void SetExceptionHandler(IExceptionHandler<T> exceptionHandler)
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
        var previousRunning = Interlocked.CompareExchange(ref _runState, ProcessorRunStates.Running, ProcessorRunStates.Idle);
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
                if (_runState == ProcessorRunStates.Running)
                {
                    ProcessEvents();
                }
            }
            finally
            {
                NotifyShutdown();
                _runState = ProcessorRunStates.Idle;
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
                var waitResult = _sequenceBarrier.WaitFor(nextSequence);
                if (waitResult.IsTimeout)
                {
                    NotifyTimeout(_sequence.Value);
                    continue;
                }

                var availableSequence = waitResult.UnsafeAvailableSequence;

                if (_onBatchStartEvaluator.ShouldInvokeOnBatchStart(availableSequence, nextSequence))
                    _eventHandler.OnBatchStart(availableSequence - nextSequence + 1);

                while (nextSequence <= availableSequence)
                {
                    var evt = _dataProvider[nextSequence];
                    _eventHandler.OnEvent(evt, nextSequence, nextSequence == availableSequence);
                    nextSequence++;
                }

                _sequence.SetValue(availableSequence);
            }
            catch (OperationCanceledException) when (_sequenceBarrier.IsCancellationRequested())
            {
                if (_runState != ProcessorRunStates.Running)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                var evt = _dataProvider[nextSequence];
                _exceptionHandler.HandleEventException(ex, nextSequence, evt);
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

    private void NotifyTimeout(long availableSequence)
    {
        try
        {
            _eventHandler.OnTimeout(availableSequence);
        }
        catch (Exception ex)
        {
            _exceptionHandler.HandleOnTimeoutException(ex, availableSequence);
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
