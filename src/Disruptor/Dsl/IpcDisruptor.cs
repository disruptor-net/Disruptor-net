using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;
using Disruptor.Util;

namespace Disruptor.Dsl;

/// <summary>
/// Represents a startable component that manages a ring buffer (see <see cref="IpcRingBuffer{T}"/>) and
/// a graph of consumers (see <see cref="IValueEventHandler{T}"/>). The ring buffer is stored in shared
/// memory (see <see cref="IpcRingBufferMemory"/>), so it can be accessed for publication by other
/// processes (see <see cref="IpcPublisher{T}"/>).
/// </summary>
/// <typeparam name="T">the type of the events, which must be an unmanaged value type.</typeparam>
/// <example>
/// <code>
/// // In the first process:
/// await using var disruptor = new IpcDisruptor&lt;MyEvent&gt;(1024);
/// var ipcDirectoryPath = disruptor.IpcDirectoryPath;
/// var handler1 = new EventHandler1();
/// var handler2 = new EventHandler2();
/// disruptor.HandleEventsWith(handler1).Then(handler2);
/// await disruptor.Start();
/// </code>
/// <code>
/// // In the second process:
/// using var publisher = new IpcPublisher&lt;MyEvent&gt;(ipcDirectoryPath);
/// using (var scope = publisher.PublishEvent())
/// {
///     scope.Event().Value = 1;
/// }
/// </code>
/// </example>
public class IpcDisruptor<T> : IDisposable, IAsyncDisposable
    where T : unmanaged
{
    private readonly IpcRingBuffer<T> _ringBuffer;
    private readonly TaskScheduler _taskScheduler;
    private readonly IpcConsumerRepository<T> _consumerRepository = new();
    private readonly ValueExceptionHandlerWrapper<T> _exceptionHandler = new();
    private readonly DisruptorState _state = new();

    public IpcDisruptor(int ringBufferSize)
        : this(ringBufferSize, new YieldingWaitStrategy())
    {
    }

    public IpcDisruptor(int ringBufferSize, IIpcWaitStrategy waitStrategy)
        : this(IpcRingBufferMemory.CreateTemporary<T>(ringBufferSize), waitStrategy, true)
    {
    }

    public IpcDisruptor(int ringBufferSize, IIpcWaitStrategy waitStrategy, TaskScheduler taskScheduler)
        : this(IpcRingBufferMemory.CreateTemporary<T>(ringBufferSize), waitStrategy, taskScheduler, true)
    {
    }

    public IpcDisruptor(IpcRingBufferMemory memory, bool ownsMemory = false)
        : this(memory, new YieldingWaitStrategy(), TaskScheduler.Default, ownsMemory)
    {
    }

    public IpcDisruptor(IpcRingBufferMemory memory, IIpcWaitStrategy waitStrategy, bool ownsMemory = false)
        : this(memory, waitStrategy, TaskScheduler.Default, ownsMemory)
    {
    }

    public IpcDisruptor(IpcRingBufferMemory memory, IIpcWaitStrategy waitStrategy, TaskScheduler taskScheduler, bool ownsMemory = false)
    {
        _ringBuffer = new IpcRingBuffer<T>(memory, waitStrategy, ownsMemory);
        _taskScheduler = taskScheduler;
    }

    /// <summary>
    /// The <see cref="IpcRingBuffer{T}"/> used by this disruptor.
    /// </summary>
    public IpcRingBuffer<T> RingBuffer => _ringBuffer;

    public string IpcDirectoryPath => _ringBuffer.IpcDirectoryPath;

    /// <summary>
    /// Get the value of the cursor indicating the published sequence.
    /// </summary>
    public long Cursor => _ringBuffer.Cursor;

    /// <summary>
    /// The capacity of the data structure to hold entries.
    /// </summary>
    public long BufferSize => _ringBuffer.BufferSize;

    /// <inheritdoc cref="IpcRingBuffer{T}.PublishEvent"/>
    public IpcRingBuffer<T>.UnpublishedEventScope PublishEvent() => RingBuffer.PublishEvent();

    /// <inheritdoc cref="IpcRingBuffer{T}.PublishEvents"/>
    public IpcRingBuffer<T>.UnpublishedEventBatchScope PublishEvents(int count) => RingBuffer.PublishEvents(count);

    /// <summary>
    /// Set up event handlers to handle events from the ring buffer. These handlers will process events
    /// as soon as they become available, in parallel.
    ///
    /// <code>disruptor.HandleEventsWith(A).Then(B);</code>
    ///
    /// This call is additive, but generally should only be called once when setting up the disruptor instance.
    /// </summary>
    /// <param name="handlers">the event handlers that will process events</param>
    /// <returns>a <see cref="IpcEventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
    public IpcEventHandlerGroup<T> HandleEventsWith(params IValueEventHandler<T>[] handlers)
    {
        return CreateEventProcessors([], handlers);
    }

    /// <summary>
    /// Specify an exception handler to be used for event handlers and worker pools created by this disruptor.
    /// The exception handler will be used by existing and future event handlers and worker pools created by this disruptor instance.
    /// </summary>
    /// <param name="exceptionHandler">the exception handler to use</param>
    public void SetDefaultExceptionHandler(IValueExceptionHandler<T> exceptionHandler)
    {
        _state.ThrowIfStartedOrDisposed();
        _exceptionHandler.SwitchTo(exceptionHandler);
    }

    /// <summary>
    /// Specify an exception handler to be used for event handlers and worker pools created by this disruptor.
    /// The exception handler will be used by existing and future event handlers and worker pools created by this disruptor instance.
    /// </summary>
    public void SetExceptionHandler(IValueEventHandler<T> eventHandler, IValueExceptionHandler<T> exceptionHandler)
    {
        _consumerRepository.GetEventProcessorFor(eventHandler).SetExceptionHandler(exceptionHandler);
    }

    /// <summary>
    /// Create a group of event handlers to be used as a dependency.
    /// For example if the handler <code>A</code> must process events before handler <code>B</code>:
    /// <code>disruptor.After(A).HandleEventsWith(B);</code>
    /// </summary>
    /// <param name="handlers">handlers the event handlers, previously set up with <see cref="HandleEventsWith(IValueEventHandler{T}[])"/>,
    /// that will form the barrier for subsequent handlers or processors.</param>
    /// <returns>an <see cref="IpcEventHandlerGroup{T}"/> that can be used to setup a dependency barrier over the specified event handlers.</returns>
    public IpcEventHandlerGroup<T> After(params IValueEventHandler<T>[] handlers)
    {
        return new IpcEventHandlerGroup<T>(this, _consumerRepository, handlers.Select(h => _consumerRepository.GetEventProcessorFor(h)).ToArray());
    }

    /// <summary>
    /// Starts the disruptor.
    /// </summary>
    /// <remarks>
    /// This method must only be called once after all event processors have been configured.
    /// </remarks>
    /// <returns>
    /// A task that represents the startup of the disruptor.
    /// The task completes after <c>OnStart</c> is invoked on every handler.
    /// </returns>
    public Task Start()
    {
        _state.Start();

        var gatingSequences = _consumerRepository.GetGatingSequences();
        _ringBuffer.SetGatingSequences(gatingSequences);

        return _consumerRepository.StartAll(_taskScheduler);
    }

    /// <summary>
    /// Halts the disruptor.
    /// </summary>
    /// <returns>
    /// A task that represents the shutdown of the disruptor.
    /// The task completes after <c>OnShutdown</c> is invoked on every handler.
    /// </returns>
    public Task Halt()
    {
        _state.Halt();

        return _consumerRepository.HaltAll();
    }

    /// <summary>
    /// Waits until all events currently in the disruptor have been processed by all event processors
    /// and then halts the disruptor. It is critical that publishing to the ring buffer has stopped
    /// before calling this method, otherwise it may never return.
    /// </summary>
    /// <returns>
    /// A task that represents the shutdown of the disruptor.
    /// The task completes after <c>OnShutdown</c> is invoked on every handler.
    /// </returns>
    public Task Shutdown()
    {
        return Shutdown(Timeout.Infinite);
    }

    /// <summary>
    /// Waits until all events currently in the disruptor have been processed by all event processors
    /// and then halts the disruptor.
    /// </summary>
    /// <param name="timeout">the amount of time to wait for all events to be processed. <code>TimeSpan.MaxValue</code> will give an infinite timeout</param>
    /// <returns>
    /// A task that represents the shutdown of the disruptor.
    /// The task completes after <c>OnShutdown</c> is invoked on every handler.
    /// </returns>
    public Task Shutdown(TimeSpan timeout)
    {
        var totalMilliseconds = (long)timeout.TotalMilliseconds;
        if (totalMilliseconds < -1 || totalMilliseconds > int.MaxValue)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException();
        }

        return Shutdown((int)totalMilliseconds);
    }

    private Task Shutdown(int millisecondsTimeout)
    {
        var timeout = millisecondsTimeout == Timeout.Infinite ? DateTime.MaxValue : DateTime.UtcNow.AddMilliseconds(millisecondsTimeout);
        var spinWait = new SpinWait();
        while (HasBacklog())
        {
            if (DateTime.UtcNow > timeout)
            {
                throw new TimeoutException();
            }

            spinWait.SpinOnce();
        }

        return Halt();
    }

    public void Dispose()
    {
        if (!_state.TryDispose())
            return;

        // The ring buffer cannot be disposed until all event processors have stopped running,
        // because the event processors could still be processing events.

        _consumerRepository.DisposeAll().Wait();

        _ringBuffer.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (!_state.TryDispose())
            return;

        await _consumerRepository.DisposeAll().ConfigureAwait(false);

        _ringBuffer.Dispose();
    }

    /// <summary>
    /// Gets the sequence value for the specified event handlers.
    /// </summary>
    /// <param name="handler">eventHandler to get the sequence for</param>
    /// <returns>eventHandler's sequence</returns>
    public long GetSequenceValueFor(IValueEventHandler<T> handler) => _consumerRepository.GetEventProcessorFor(handler).SequencePointer.Value;

    /// <summary>
    /// Confirms if all messages have been consumed by all event processors.
    /// </summary>
    /// <returns></returns>
    private bool HasBacklog()
    {
        var cursor = _ringBuffer.Cursor;
        return _consumerRepository.HasBacklog(cursor, false);
    }

    /// <summary>
    /// Indicates whether the disruptor has been started.
    /// </summary>
    public bool HasStarted => _state.HasStarted;

    /// <summary>
    /// Indicates whether the disruptor is running.
    /// </summary>
    public bool IsRunning => _state.IsRunning;

    internal IpcEventHandlerGroup<T> CreateEventProcessors(IIpcEventProcessor<T>[] previousEventProcessors, IValueEventHandler<T>[] eventHandlers)
    {
        _state.ThrowIfStartedOrDisposed();

        var eventProcessors = new IIpcEventProcessor<T>[eventHandlers.Length];
        var barrierEventProcessors = previousEventProcessors.Select(x => x.SequencePointer).ToArray();

        for (int i = 0; i < eventHandlers.Length; i++)
        {
            var eventHandler = eventHandlers[i];

            var sequence = _ringBuffer.NewSequence();
            sequence.SetValue(_ringBuffer.Cursor);

            var sequenceWaiterOwner = SequenceWaiterOwner.EventHandler(eventHandler);
            var barrier = _ringBuffer.NewBarrier(sequenceWaiterOwner, barrierEventProcessors);

            var eventProcessor = EventProcessorFactory.Create(_ringBuffer, sequence, barrier, eventHandler);
            eventProcessor.SetExceptionHandler(_exceptionHandler);

            _consumerRepository.Add(eventProcessor, eventHandler);
            eventProcessors[i] = eventProcessor;
        }

        _consumerRepository.UnMarkEventProcessorsAsEndOfChain(previousEventProcessors);

        return new IpcEventHandlerGroup<T>(this, _consumerRepository, eventProcessors);
    }


    public override string ToString()
    {
        return $"IpcDisruptor {{RingBuffer={_ringBuffer}, State={_state}}}";
    }
}
