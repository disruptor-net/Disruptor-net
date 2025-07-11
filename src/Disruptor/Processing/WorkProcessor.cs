using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Disruptor.Util;

namespace Disruptor.Processing;

/// <summary>
/// Event processor that runs a <see cref="IWorkHandler{T}"/>.
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
    private readonly EventProcessorState _state = new(restartable: false);

    /// <summary>
    /// Construct a <see cref="WorkProcessor{T}"/>.
    /// </summary>
    /// <param name="ringBuffer">ringBuffer to which events are published.</param>
    /// <param name="sequenceBarrier">sequenceBarrier on which it is waiting.</param>
    /// <param name="workHandler">workHandler is the delegate to which events are dispatched.</param>
    /// <param name="exceptionHandler">exceptionHandler to be called back when an error occurs</param>
    /// <param name="workSequence">workSequence from which to claim the next event to be worked on</param>
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

    /// <inheritdoc/>
    public Sequence Sequence => _sequence;

    public DependentSequenceGroup DependentSequences => _sequenceBarrier.DependentSequences;

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
    public Task Start(TaskScheduler taskScheduler)
    {
        var runState = _state.Start();
        taskScheduler.StartLongRunningTask(() => Run(runState));

        return runState.StartTask;
    }

    [MethodImpl(Constants.AggressiveOptimization)]
    private void Run(EventProcessorState.RunState runState)
    {
        NotifyStart(runState);

        var cancellationToken = runState.CancellationToken;
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
                    if (runState.IsHalted)
                    {
                        _sequenceBarrier.CancelProcessing();
                        break;
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
                    var waitResult = _sequenceBarrier.WaitForPublishedSequence(nextSequence, cancellationToken);
                    if (waitResult.IsTimeout)
                    {
                        NotifyTimeout(_sequence.Value);
                        continue;
                    }

                    cachedAvailableSequence = waitResult.UnsafeAvailableSequence;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                var evt = _ringBuffer[nextSequence];
                _exceptionHandler.HandleEventException(ex, nextSequence, evt);
                processedSequence = true;
            }
        }

        NotifyShutdown(runState);
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

    private void NotifyStart(EventProcessorState.RunState runState)
    {
        try
        {
            _workHandler.OnStart();
        }
        catch (Exception ex)
        {
            _exceptionHandler.HandleOnStartException(ex);
        }

        runState.OnStarted();
    }

    private void NotifyShutdown(EventProcessorState.RunState runState)
    {
        try
        {
            _workHandler.OnShutdown();
        }
        catch (Exception ex)
        {
            _exceptionHandler.HandleOnShutdownException(ex);
        }

        runState.OnShutdown();
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
