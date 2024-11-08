using System;
using System.Diagnostics;
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
    private volatile int _runState = ProcessorRunStates.Idle;
    private readonly Sequence _workSequence = new();
    private readonly RingBuffer<T> _ringBuffer;
    // WorkProcessors are created to wrap each of the provided WorkHandlers
    private readonly WorkProcessor<T>[] _workProcessors;

    /// <summary>
    /// Create a worker pool to enable an array of <see cref="IWorkHandler{T}"/>s to consume published sequences.
    ///
    /// This option requires a pre-configured <see cref="RingBuffer{T}"/> which must have <see cref="ISequencer.AddGatingSequences"/>
    /// called before the work pool is started.
    /// </summary>
    /// <param name="ringBuffer">ringBuffer of events to be consumed.</param>
    /// <param name="sequenceBarrier">sequenceBarrier on which the workers will depend.</param>
    /// <param name="exceptionHandler">exceptionHandler to callback when an error occurs which is not handled by the <see cref="IWorkHandler{T}"/>s.</param>
    /// <param name="workHandlers">workHandlers to distribute the work load across.</param>
    public WorkerPool(RingBuffer<T> ringBuffer, SequenceBarrier sequenceBarrier, IExceptionHandler<T> exceptionHandler, params IWorkHandler<T>[] workHandlers)
    {
        _ringBuffer = ringBuffer;
        _workProcessors = new WorkProcessor<T>[workHandlers.Length];

        for (var i = 0; i < workHandlers.Length; i++)
        {
            _workProcessors[i] = new WorkProcessor<T>(
                ringBuffer,
                sequenceBarrier,
                workHandlers[i],
                exceptionHandler,
                _workSequence);
        }
    }

    /// <summary>
    /// Construct a work pool with an internal <see cref="RingBuffer{T}"/> for convenience.
    ///
    /// This option does not require <see cref="ISequencer.AddGatingSequences"/> to be called before the work pool is started.
    /// </summary>
    /// <param name="eventFactory">eventFactory for filling the <see cref="RingBuffer{T}"/></param>
    /// <param name="exceptionHandler">exceptionHandler to callback when an error occurs which is not handled by the <see cref="IWorkHandler{T}"/>s.</param>
    /// <param name="workHandlers">workHandlers to distribute the work load across.</param>
    public WorkerPool(Func<T> eventFactory, IExceptionHandler<T> exceptionHandler, params IWorkHandler<T>[] workHandlers)
    {
        _ringBuffer = RingBuffer<T>.CreateMultiProducer(eventFactory, 1024, new BlockingWaitStrategy());
        var barrier = _ringBuffer.NewBarrier();
        _workProcessors = new WorkProcessor<T>[workHandlers.Length];

        for (var i = 0; i < workHandlers.Length; i++)
        {
            _workProcessors[i] = new WorkProcessor<T>(
                _ringBuffer,
                barrier,
                workHandlers[i],
                exceptionHandler,
                _workSequence);
        }

        _ringBuffer.AddGatingSequences(GetWorkerSequences());
    }

    /// <summary>
    /// The <see cref="RingBuffer{T}"/> used by this worker pool.
    /// </summary>
    public RingBuffer<T> RingBuffer => _ringBuffer;

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
    /// Waits before the event processors are started.
    /// </summary>
    /// <param name="timeout">maximum wait duration</param>
    public void WaitUntilStarted(TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();

        foreach (var workProcessor in _workProcessors)
        {
            var elapsed = stopwatch.Elapsed;
            var remaining = timeout >= elapsed ? timeout - elapsed : TimeSpan.Zero;
            workProcessor.WaitUntilStarted(remaining);
        }
    }

    /// <summary>
    /// Start the worker pool processing events in sequence.
    /// </summary>
    /// <exception cref="InvalidOperationException">if the pool is already started or halted</exception>
    public RingBuffer<T> Start()
    {
        return Start(TaskScheduler.Default);
    }

    /// <summary>
    /// Start the worker pool processing events in sequence.
    /// </summary>
    /// <exception cref="InvalidOperationException">if the pool is already started or halted</exception>
    public RingBuffer<T> Start(TaskScheduler taskScheduler)
    {
        var previousRunState = Interlocked.CompareExchange(ref _runState, ProcessorRunStates.Running, ProcessorRunStates.Idle);
        if (previousRunState == ProcessorRunStates.Running)
        {
            throw new InvalidOperationException("WorkerPool is already running");
        }

        if (previousRunState == ProcessorRunStates.Halted)
        {
            throw new InvalidOperationException("WorkerPool is halted and cannot be restarted");
        }

        var cursor = _ringBuffer.Cursor;
        _workSequence.SetValue(cursor);

        foreach (var workProcessor in _workProcessors)
        {
            workProcessor.Sequence.SetValue(cursor);
            workProcessor.StartLongRunning(taskScheduler);
        }

        return _ringBuffer;
    }

    /// <summary>
    /// Wait for the <see cref="RingBuffer{T}"/> to drain of published events then halt the workers.
    /// </summary>
    public void DrainAndHalt()
    {
        var workerSequences = GetWorkerSequences();
        while (_ringBuffer.Cursor > DisruptorUtil.GetMinimumSequence(workerSequences))
        {
            Thread.Sleep(0);
        }

        foreach (var workProcessor in _workProcessors)
        {
            workProcessor.Halt();
        }

        _runState = ProcessorRunStates.Halted;
    }

    /// <summary>
    /// Halt all workers immediately at then end of their current cycle.
    /// </summary>
    public void Halt()
    {
        foreach (var workProcessor in _workProcessors)
        {
            workProcessor.Halt();
        }

        _runState = ProcessorRunStates.Halted;
    }

    public bool IsRunning => _runState == ProcessorRunStates.Running;
}
