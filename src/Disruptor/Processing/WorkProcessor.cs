using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Util;

namespace Disruptor.Processing;

/// <summary>
/// A <see cref="WorkProcessor{T}"/> wraps a single <see cref="IWorkHandler{T}"/>, effectively consuming the sequence and ensuring appropriate barriers.
///
/// Generally, this will be used as part of a <see cref="WorkerPool{T}"/>.
/// </summary>
/// <typeparam name="T">event implementation storing the details for the work to processed.</typeparam>
public sealed class WorkProcessor<T> : IEventProcessor
    where T : class
{
    private readonly Sequence _sequence = new();
    private readonly RingBuffer<T> _ringBuffer;
    private readonly SequenceBarrier _sequenceBarrier;
    private readonly IWorkHandler<T> _workHandler;
    private readonly IExceptionHandler<T> _exceptionHandler;
    private readonly Sequence _workSequence;
    private readonly ManualResetEventSlim _started = new();
    private volatile int _runState = ProcessorRunStates.Idle;

    /// <summary>
    /// Construct a <see cref="WorkProcessor{T}"/>.
    /// </summary>
    /// <param name="ringBuffer">ringBuffer to which events are published.</param>
    /// <param name="sequenceBarrier">sequenceBarrier on which it is waiting.</param>
    /// <param name="workHandler">workHandler is the delegate to which events are dispatched.</param>
    /// <param name="exceptionHandler">exceptionHandler to be called back when an error occurs</param>
    /// <param name="workSequence">workSequence from which to claim the next event to be worked on.  It should always be initialised
    /// as <see cref="Disruptor.Sequence.InitialCursorValue"/></param>
    public WorkProcessor(RingBuffer<T> ringBuffer, SequenceBarrier sequenceBarrier, IWorkHandler<T> workHandler, IExceptionHandler<T> exceptionHandler, Sequence workSequence)
    {
        _ringBuffer = ringBuffer;
        _sequenceBarrier = sequenceBarrier;
        _workHandler = workHandler;
        _exceptionHandler = exceptionHandler;
        _workSequence = workSequence;

        // TODO: Move to IWorkHandler with default implementation.
        if (_workHandler is IEventReleaseAware eventReleaseAware)
            eventReleaseAware.SetEventReleaser(new EventReleaser(this));
    }

    /// <summary>
    /// <see cref="IEventProcessor.Sequence"/>.
    /// </summary>
    public Sequence Sequence => _sequence;

    /// <summary>
    /// <see cref="IEventProcessor.Halt"/>.
    /// </summary>
    public void Halt()
    {
        _runState = ProcessorRunStates.Halted;
        _sequenceBarrier.CancelProcessing();
    }

    /// <summary>
    /// Signal that this <see cref="WorkProcessor{T}"/> should stop when it has finished processing its work sequence.
    /// </summary>
    public void HaltLater()
    {
        _runState = ProcessorRunStates.Halted;
    }

    /// <summary>
    /// <see cref="IEventProcessor.IsRunning"/>
    /// </summary>
    public bool IsRunning => _runState == ProcessorRunStates.Running;

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

    [MethodImpl(Constants.AggressiveOptimization)]
    public void Run()
    {
        var previousRunState = Interlocked.CompareExchange(ref _runState, ProcessorRunStates.Running, ProcessorRunStates.Idle);
        if (previousRunState == ProcessorRunStates.Running)
        {
            throw new InvalidOperationException("WorkProcessor is already running");
        }

        if (previousRunState == ProcessorRunStates.Halted)
        {
            throw new InvalidOperationException("WorkProcessor is halted and cannot be restarted");
        }

        _sequenceBarrier.ResetProcessing();

        NotifyStart();

        var processedSequence = true;
        var cachedAvailableSequence = long.MinValue;
        var nextSequence = _sequence.Value;

        while (true)
        {
            try
            {
                // if previous sequence was processed - fetch the next sequence and set
                // that we have successfully processed the previous sequence
                // typically, this will be true
                // this prevents the sequence getting too far forward if an exception
                // is thrown from the WorkHandler

                if (processedSequence)
                {
                    if (_runState != ProcessorRunStates.Running)
                    {
                        _sequenceBarrier.CancelProcessing();
                        _sequenceBarrier.ThrowIfCancellationRequested();
                    }

                    processedSequence = false;
                    do
                    {
                        nextSequence = _workSequence.Value + 1L;
                        _sequence.SetValue(nextSequence - 1L);
                    }
                    while (!_workSequence.CompareAndSet(nextSequence - 1L, nextSequence));
                }

                if (cachedAvailableSequence >= nextSequence)
                {
                    var evt = _ringBuffer[nextSequence];
                    _workHandler.OnEvent(evt);
                    processedSequence = true;
                }
                else
                {
                    var waitResult = _sequenceBarrier.WaitFor(nextSequence);
                    if (waitResult.IsTimeout)
                    {
                        NotifyTimeout(_sequence.Value);
                        continue;
                    }

                    cachedAvailableSequence = waitResult.UnsafeAvailableSequence;
                }
            }
            catch (OperationCanceledException) when (_sequenceBarrier.IsCancellationRequested)
            {
                if (_runState != ProcessorRunStates.Running)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                var evt = _ringBuffer[nextSequence];
                _exceptionHandler.HandleEventException(ex, nextSequence, evt);
                processedSequence = true;
            }
        }

        NotifyShutdown();

        _runState = ProcessorRunStates.Halted;
    }

    private void NotifyTimeout(long availableSequence)
    {
        try
        {
            _workHandler.OnTimeout(availableSequence);
        }
        catch (Exception ex)
        {
            _exceptionHandler.HandleOnTimeoutException(ex, availableSequence);
        }
    }

    private void NotifyStart()
    {
        try
        {
            _workHandler.OnStart();
        }
        catch (Exception ex)
        {
            _exceptionHandler.HandleOnStartException(ex);
        }

        _started.Set();
    }

    private void NotifyShutdown()
    {
        try
        {
            _workHandler.OnShutdown();
        }
        catch (Exception ex)
        {
            _exceptionHandler.HandleOnShutdownException(ex);
        }

        _started.Reset();
    }

    private class EventReleaser : IEventReleaser
    {
        private readonly WorkProcessor<T> _workProcessor;

        public EventReleaser(WorkProcessor<T> workProcessor)
        {
            _workProcessor = workProcessor;
        }

        public void Release()
        {
            _workProcessor._sequence.SetValue(long.MaxValue);
        }
    }
}
