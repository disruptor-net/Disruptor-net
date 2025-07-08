using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor.Processing;

/// <summary>
/// WorkerPool contains a pool of <see cref="WorkProcessor{T}"/> that will consume sequences so jobs can be farmed out across a pool of workers.
/// Each of the <see cref="WorkProcessor{T}"/> manage and calls a <see cref="IWorkHandler{T}"/> to process the events.
///
/// Once a WorkerPool has been halted it cannot be started again.
/// </summary>
/// <typeparam name="T">event to be processed by a pool of workers</typeparam>
public sealed class WorkerPool<T> where T : class
{
    private readonly EventProcessorState _state = new(restartable: false);
    private readonly Sequence _workSequence = new();
    private readonly RingBuffer<T> _ringBuffer;
    private readonly WorkProcessor<T>[] _workProcessors;

    /// <summary>
    /// Create a worker pool to enable an array of <see cref="IWorkHandler{T}"/>s to consume published sequences.
    ///
    /// This option requires a pre-configured <see cref="RingBuffer{T}"/> which must have <see cref="ISequencer.AddGatingSequences"/>
    /// called before the work pool is started.
    /// </summary>
    /// <param name="ringBuffer">ringBuffer of events to be consumed.</param>
    /// <param name="barrierSequences">sequences of the processors that must run before</param>
    /// <param name="exceptionHandler">exceptionHandler to callback when an error occurs which is not handled by the <see cref="IWorkHandler{T}"/>s.</param>
    /// <param name="workHandlers">workHandlers to distribute the work load across.</param>
    public WorkerPool(RingBuffer<T> ringBuffer, Sequence[] barrierSequences, IExceptionHandler<T> exceptionHandler, params IWorkHandler<T>[] workHandlers)
    {
        if (workHandlers.Length == 0)
            throw new ArgumentException("Unable to create worker pool without any work handlers.");

        _ringBuffer = ringBuffer;
        _workProcessors = workHandlers.Select(x => CreateWorkProcessor(x, _workSequence)).ToArray();

        DependentSequences = _workProcessors[0].DependentSequences;

        WorkProcessor<T> CreateWorkProcessor(IWorkHandler<T> workHandler, Sequence workSequence)
        {
            var sequenceBarrier = ringBuffer.NewBarrier(SequenceWaiterOwner.WorkHandler(workHandler), barrierSequences);
            return new WorkProcessor<T>(ringBuffer, sequenceBarrier, workHandler, exceptionHandler, workSequence);
        }
    }

    /// <summary>
    /// Create a worker pool to enable an array of <see cref="IWorkHandler{T}"/>s to consume published sequences.
    ///
    /// This option requires a pre-configured <see cref="RingBuffer{T}"/> which must have <see cref="ISequencer.AddGatingSequences"/>
    /// called before the work pool is started.
    /// </summary>
    /// <param name="ringBuffer">ringBuffer of events to be consumed.</param>
    /// <param name="workProcessors">event processors of the target <see cref="IWorkHandler{T}"/>.</param>
    public WorkerPool(RingBuffer<T> ringBuffer, WorkProcessor<T>[] workProcessors)
    {
        if (workProcessors.Length == 0)
            throw new ArgumentException("Unable to create worker pool without any work processors.");

        _ringBuffer = ringBuffer;
        _workProcessors = workProcessors;

        DependentSequences = _workProcessors[0].DependentSequences;
    }

    public DependentSequenceGroup DependentSequences { get; }

    /// <summary>
    /// Get an array of <see cref="Sequence"/>s representing the progress of the workers.
    /// </summary>
    public Sequence[] GetWorkerSequences()
    {
        var sequences = new Sequence[_workProcessors.Length + 1];
        for (var i = 0; i < _workProcessors.Length; i++)
        {
            sequences[i] = _workProcessors[i].Sequence;
        }
        sequences[sequences.Length - 1] = _workSequence;

        return sequences;
    }

    /// <summary>
    /// Start the worker pool processing events in sequence.
    /// </summary>
    /// <exception cref="InvalidOperationException">if the pool is already started or halted</exception>
    public Task Start()
    {
        return Start(TaskScheduler.Default);
    }

    /// <summary>
    /// Start the worker pool processing events in sequence.
    /// </summary>
    /// <exception cref="InvalidOperationException">if the pool is already started or halted</exception>
    public Task Start(TaskScheduler taskScheduler)
    {
        _state.Start();

        var cursor = _ringBuffer.Cursor;
        _workSequence.SetValue(cursor);

        var startTasks = new List<Task>(_workProcessors.Length);

        foreach (var workProcessor in _workProcessors)
        {
            workProcessor.Sequence.SetValue(cursor);
            startTasks.Add(workProcessor.StartLongRunning(taskScheduler));
        }

        return Task.WhenAll(startTasks);
    }

    /// <summary>
    /// Halt all workers immediately at then end of their current cycle.
    /// </summary>
    public Task Halt()
    {
        _state.Halt();

        var haltTasks = new List<Task>(_workProcessors.Length);

        foreach (var workProcessor in _workProcessors)
        {
            haltTasks.Add(workProcessor.Halt());
        }

        return Task.WhenAll(haltTasks);
    }

    public bool IsRunning
    {
        get
        {
            foreach (var workProcessor in _workProcessors)
            {
                if (workProcessor.IsRunning)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Indicates whether all messages have been consumed.
    /// </summary>
    public bool HasBacklog()
    {
        var cursor  = _ringBuffer.Cursor;
        foreach (var workProcessor in _workProcessors)
        {
            if (cursor > workProcessor.Sequence.Value)
            {
                return true;
            }
        }

        return false;
    }
}
