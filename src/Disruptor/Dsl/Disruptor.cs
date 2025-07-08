using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;

namespace Disruptor.Dsl;

/// <summary>
/// A DSL-style API for setting up the disruptor pattern around a ring buffer
/// (aka the Builder pattern).
///
/// A simple example of setting up the disruptor with two event handlers that
/// must process events in order:
/// <code>var disruptor = new Disruptor{MyEvent}(() => new MyEvent(), 32, TaskScheduler.Default);
/// var handler1 = new EventHandler1() { ... };
/// var handler2 = new EventHandler2() { ... };
/// disruptor.HandleEventsWith(handler1).Then(handler2);
///
/// var ringBuffer = disruptor.Start();</code>
/// </summary>
/// <typeparam name="T">the type of event used.</typeparam>
public class Disruptor<T>
    where T : class
{
    private readonly RingBuffer<T> _ringBuffer;
    private readonly TaskScheduler _taskScheduler;
    private readonly ConsumerRepository _consumerRepository = new();
    private readonly ExceptionHandlerWrapper<T> _exceptionHandler = new();
    private volatile int _started;

    /// <summary>
    /// Create a new Disruptor using <see cref="SequencerFactory.DefaultWaitStrategy"/> and <see cref="SequencerFactory.DefaultProducerType"/>.
    /// </summary>
    /// <param name="eventFactory">the factory to create events in the ring buffer</param>
    /// <param name="ringBufferSize">the size of the ring buffer, must be power of 2</param>
    public Disruptor(Func<T> eventFactory, int ringBufferSize)
        : this(eventFactory, ringBufferSize, TaskScheduler.Default)
    {
    }

    /// <summary>
    /// Create a new Disruptor using <see cref="SequencerFactory.DefaultWaitStrategy"/> and <see cref="SequencerFactory.DefaultProducerType"/>.
    /// </summary>
    /// <param name="eventFactory">the factory to create events in the ring buffer</param>
    /// <param name="ringBufferSize">the size of the ring buffer, must be power of 2</param>
    /// <param name="taskScheduler">a <see cref="TaskScheduler"/> to create threads for processors</param>
    public Disruptor(Func<T> eventFactory, int ringBufferSize, TaskScheduler taskScheduler)
        : this(eventFactory, ringBufferSize, taskScheduler, SequencerFactory.DefaultProducerType, SequencerFactory.DefaultWaitStrategy())
    {
    }

    /// <summary>
    /// Create a new Disruptor using <see cref="SequencerFactory.DefaultProducerType"/>.
    /// </summary>
    /// <param name="eventFactory">the factory to create events in the ring buffer</param>
    /// <param name="ringBufferSize">the size of the ring buffer, must be power of 2</param>
    /// <param name="waitStrategy">the wait strategy to use for the ring buffer</param>
    public Disruptor(Func<T> eventFactory, int ringBufferSize, IWaitStrategy waitStrategy)
        : this(eventFactory, ringBufferSize, TaskScheduler.Default, SequencerFactory.DefaultProducerType, waitStrategy)
    {
    }

    /// <summary>
    /// Create a new Disruptor.
    /// </summary>
    /// <param name="eventFactory">the factory to create events in the ring buffer</param>
    /// <param name="ringBufferSize">the size of the ring buffer, must be power of 2</param>
    /// <param name="taskScheduler">a <see cref="TaskScheduler"/> to create threads for processors</param>
    /// <param name="producerType">the claim strategy to use for the ring buffer</param>
    /// <param name="waitStrategy">the wait strategy to use for the ring buffer</param>
    public Disruptor(Func<T> eventFactory, int ringBufferSize, TaskScheduler taskScheduler, ProducerType producerType, IWaitStrategy waitStrategy)
    {
        _ringBuffer = new RingBuffer<T>(eventFactory, SequencerFactory.Create(producerType, ringBufferSize, waitStrategy));
        _taskScheduler = taskScheduler;
    }

    /// <summary>
    /// Set up event handlers to handle events from the ring buffer. These handlers will process events
    /// as soon as they become available, in parallel.
    ///
    /// <code>dw.HandleEventsWith(A).Then(B);</code>
    ///
    /// This call is additive, but generally should only be called once when setting up the disruptor instance.
    /// </summary>
    /// <param name="handlers">the event handlers that will process events</param>
    /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
    public EventHandlerGroup<T> HandleEventsWith(params IEventHandler<T>[] handlers)
    {
        return CreateEventProcessors(Array.Empty<Sequence>(), handlers);
    }

    /// <summary>
    /// Set up event handlers to handle events from the ring buffer. These handlers will process events
    /// as soon as they become available, in parallel.
    ///
    /// <code>dw.HandleEventsWith(A).Then(B);</code>
    ///
    /// This call is additive, but generally should only be called once when setting up the disruptor instance.
    /// </summary>
    /// <param name="handlers">the event handlers that will process events</param>
    /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
    public EventHandlerGroup<T> HandleEventsWith(params IBatchEventHandler<T>[] handlers)
    {
        return CreateEventProcessors(Array.Empty<Sequence>(), handlers);
    }

    /// <summary>
    /// Set up event handlers to handle events from the ring buffer. These handlers will process events
    /// as soon as they become available, in parallel.
    ///
    /// <code>dw.HandleEventsWith(A).Then(B);</code>
    ///
    /// This call is additive, but generally should only be called once when setting up the disruptor instance.
    /// </summary>
    /// <param name="handlers">the event handlers that will process events</param>
    /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
    public EventHandlerGroup<T> HandleEventsWith(params IAsyncBatchEventHandler<T>[] handlers)
    {
        return CreateEventProcessors(Array.Empty<Sequence>(), handlers);
    }

    /// <summary>
    /// Set up custom event processors to handle events from the ring buffer. The disruptor will
    /// automatically start these processors when <see cref="Start"/> is called.
    ///
    /// This method can be used as the start of a chain. For example if the handler <code>A</code> must
    /// process events before handler<code>B</code>:
    /// <code>dw.HandleEventsWith(A).Then(B);</code>
    ///
    /// Since this is the start of the chain, the processor factories will always be passed an empty <code>Sequence</code>
    /// array, so the factory isn't necessary in this case. This method is provided for consistency with
    /// <see cref="EventHandlerGroup{T}.HandleEventsWith(EventProcessorCreator{T}[])"/> and <see cref="EventHandlerGroup{T}.Then(EventProcessorCreator{T}[])"/>
    /// which do have barrier sequences to provide.
    ///
    /// This call is additive, but generally should only be called once when setting up the disruptor instance.
    /// </summary>
    /// <param name="eventProcessorFactories">eventProcessorFactories the event processor factories to use to create the event processors that will process events.</param>
    /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
    public EventHandlerGroup<T> HandleEventsWith(params EventProcessorCreator<T>[] eventProcessorFactories)
    {
        return CreateEventProcessors(Array.Empty<Sequence>(), eventProcessorFactories);
    }

    /// <summary>
    /// Set up custom event processors to handle events from the ring buffer. The disruptor will
    /// automatically start this processors when <see cref="Start"/> is called.
    ///
    /// This method can be used as the start of a chain. For example if the processor <code>A</code> must
    /// process events before handler<code>B</code>:
    /// <code>dw.HandleEventsWith(A).Then(B);</code>
    /// </summary>
    /// <param name="processors">processors the event processors that will process events</param>
    /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
    public EventHandlerGroup<T> HandleEventsWith(params IEventProcessor[] processors)
    {
        foreach (var processor in processors)
        {
            _consumerRepository.Add(processor);
        }

        var sequences = new Sequence[processors.Length];
        for (int i = 0; i < processors.Length; i++)
        {
            sequences[i] = processors[i].Sequence;
        }

        _ringBuffer.AddGatingSequences(sequences);

        return new EventHandlerGroup<T>(this, _consumerRepository, DisruptorUtil.GetSequencesFor(processors));
    }

    /// <summary>
    /// Set up a <see cref="WorkerPool{T}"/> to distribute an event to one of a pool of work handler threads.
    /// Each event will only be processed by one of the work handlers.
    /// The disruptor will automatically start this processors when <see cref="Start"/> is called.
    /// </summary>
    /// <param name="workHandlers">the work handlers that will process events.</param>
    /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
    public EventHandlerGroup<T> HandleEventsWithWorkerPool(params IWorkHandler<T>[] workHandlers)
    {
        return CreateWorkerPool(Array.Empty<Sequence>(), workHandlers);
    }

    /// <summary>
    /// Specify an exception handler to be used for event handlers and worker pools created by this disruptor.
    /// The exception handler will be used by existing and future event handlers and worker pools created by this disruptor instance.
    /// </summary>
    /// <param name="exceptionHandler">the exception handler to use</param>
    public void SetDefaultExceptionHandler(IExceptionHandler<T> exceptionHandler)
    {
        CheckNotStarted();
        _exceptionHandler.SwitchTo(exceptionHandler);
    }

    /// <summary>
    /// Override the default exception handler for a specific handler.
    /// <code>disruptorWizard.HandleExceptionsIn(eventHandler).With(exceptionHandler);</code>
    /// </summary>
    /// <param name="eventHandler">eventHandler the event handler to set a different exception handler for</param>
    /// <returns>an <see cref="ExceptionHandlerSetting{T}"/> dsl object - intended to be used by chaining the with method call</returns>
    public ExceptionHandlerSetting<T> HandleExceptionsFor(IEventHandler<T> eventHandler)
    {
        return new ExceptionHandlerSetting<T>(eventHandler, _consumerRepository);
    }

    /// <summary>
    /// Override the default exception handler for a specific handler.
    /// <code>disruptorWizard.HandleExceptionsIn(eventHandler).With(exceptionHandler);</code>
    /// </summary>
    /// <param name="eventHandler">eventHandler the event handler to set a different exception handler for</param>
    /// <returns>an <see cref="ExceptionHandlerSetting{T}"/> dsl object - intended to be used by chaining the with method call</returns>
    public ExceptionHandlerSetting<T> HandleExceptionsFor(IBatchEventHandler<T> eventHandler)
    {
        return new ExceptionHandlerSetting<T>(eventHandler, _consumerRepository);
    }

    /// <summary>
    /// Override the default exception handler for a specific handler.
    /// <code>disruptorWizard.HandleExceptionsIn(eventHandler).With(exceptionHandler);</code>
    /// </summary>
    /// <param name="eventHandler">eventHandler the event handler to set a different exception handler for</param>
    /// <returns>an <see cref="ExceptionHandlerSetting{T}"/> dsl object - intended to be used by chaining the with method call</returns>
    public ExceptionHandlerSetting<T> HandleExceptionsFor(IAsyncBatchEventHandler<T> eventHandler)
    {
        return new ExceptionHandlerSetting<T>(eventHandler, _consumerRepository);
    }

    /// <summary>
    /// Create a group of event handlers to be used as a dependency.
    /// For example if the handler <code>A</code> must process events before handler <code>B</code>:
    /// <code>dw.After(A).HandleEventsWith(B);</code>
    /// </summary>
    /// <param name="handlers">handlers the event handlers, previously set up with <see cref="HandleEventsWith(Disruptor.IEventHandler{T}[])"/>,
    /// that will form the barrier for subsequent handlers or processors.</param>
    /// <returns>an <see cref="EventHandlerGroup{T}"/> that can be used to setup a dependency barrier over the specified event handlers.</returns>
    public EventHandlerGroup<T> After(params IEventHandler<T>[] handlers)
    {
        return new EventHandlerGroup<T>(this, _consumerRepository, handlers.Select(h => _consumerRepository.GetSequenceFor(h)));
    }

    /// <summary>
    /// Create a group of event handlers to be used as a dependency.
    /// For example if the handler <code>A</code> must process events before handler <code>B</code>:
    /// <code>dw.After(A).HandleEventsWith(B);</code>
    /// </summary>
    /// <param name="handlers">handlers the event handlers, previously set up with <see cref="HandleEventsWith(Disruptor.IEventHandler{T}[])"/>,
    /// that will form the barrier for subsequent handlers or processors.</param>
    /// <returns>an <see cref="EventHandlerGroup{T}"/> that can be used to setup a dependency barrier over the specified event handlers.</returns>
    public EventHandlerGroup<T> After(params IBatchEventHandler<T>[] handlers)
    {
        return new EventHandlerGroup<T>(this, _consumerRepository, handlers.Select(h => _consumerRepository.GetSequenceFor(h)));
    }

    /// <summary>
    /// Create a group of event handlers to be used as a dependency.
    /// For example if the handler <code>A</code> must process events before handler <code>B</code>:
    /// <code>dw.After(A).HandleEventsWith(B);</code>
    /// </summary>
    /// <param name="handlers">handlers the event handlers, previously set up with <see cref="HandleEventsWith(Disruptor.IEventHandler{T}[])"/>,
    /// that will form the barrier for subsequent handlers or processors.</param>
    /// <returns>an <see cref="EventHandlerGroup{T}"/> that can be used to setup a dependency barrier over the specified event handlers.</returns>
    public EventHandlerGroup<T> After(params IAsyncBatchEventHandler<T>[] handlers)
    {
        return new EventHandlerGroup<T>(this, _consumerRepository, handlers.Select(h => _consumerRepository.GetSequenceFor(h)));
    }

    /// <summary>
    /// Create a group of event processors to be used as a dependency.
    /// </summary>
    /// <seealso cref="After(Disruptor.IEventHandler{T}[])"/>
    /// <param name="processors">processors the event processors, previously set up with <see cref="HandleEventsWith(Disruptor.IEventHandler{T}[])"/>,
    /// that will form the barrier for subsequent handlers or processors.</param>
    /// <returns>an <see cref="EventHandlerGroup{T}"/> that can be used to setup a <see cref="SequenceBarrier"/> over the specified event processors.</returns>
    public EventHandlerGroup<T> After(params IEventProcessor[] processors)
    {
        return new EventHandlerGroup<T>(this, _consumerRepository, DisruptorUtil.GetSequencesFor(processors));
    }

    /// <inheritdoc cref="RingBuffer{T}.PublishEvent()"/>.
    public RingBuffer<T>.UnpublishedEventScope PublishEvent() => _ringBuffer.PublishEvent();

    /// <inheritdoc cref="RingBuffer{T}.PublishEvents(int)"/>.
    public RingBuffer<T>.UnpublishedEventBatchScope PublishEvents(int count) => _ringBuffer.PublishEvents(count);

    /// <summary>
    /// Starts the event processors and returns the fully configured ring buffer.
    ///
    /// The ring buffer is set up to prevent overwriting any entry that is yet to
    /// be processed by the slowest event processor.
    ///
    /// This method must only be called once after all event processors have been added.
    /// </summary>
    public void Start()
    {
        CheckOnlyStartedOnce();
        foreach (var consumerInfo in _consumerRepository.Consumers)
        {
            consumerInfo.Start(_taskScheduler);
        }
    }

    /// <summary>
    /// Calls <see cref="IEventProcessor.Halt"/> on all the event processors created via this disruptor.
    /// </summary>
    public void Halt()
    {
        if (_started == 0)
        {
            // Preserve previous behavior: ignore Halt if the Disruptor is not started
            return;
        }

        foreach (var consumerInfo in _consumerRepository.Consumers)
        {
            consumerInfo.Halt();
        }
    }

    /// <summary>
    /// Waits until all events currently in the disruptor have been processed by all event processors
    /// and then halts the processors.It is critical that publishing to the ring buffer has stopped
    /// before calling this method, otherwise it may never return.
    ///
    /// This method will not await the final termination of the processor threads.
    /// </summary>
    public void Shutdown()
    {
        try
        {
            Shutdown(TimeSpan.FromMilliseconds(-1)); // do not wait
        }
        catch (TimeoutException e)
        {
            _exceptionHandler.HandleOnShutdownException(e);
        }
    }

    /// <summary>
    /// Waits until all events currently in the disruptor have been processed by all event processors
    /// and then halts the processors.
    ///
    /// This method will not await the final termination of the processor threads.
    /// </summary>
    /// <param name="timeout">the amount of time to wait for all events to be processed. <code>TimeSpan.MaxValue</code> will give an infinite timeout</param>
    /// <exception cref="TimeoutException">if a timeout occurs before shutdown completes.</exception>
    public void Shutdown(TimeSpan timeout)
    {
        var timeoutAt = DateTime.UtcNow.Add(timeout);
        while (HasBacklog())
        {
            if (timeout.Ticks >= 0 && DateTime.UtcNow > timeoutAt)
            {
                throw new TimeoutException();
            }
            // Busy spin
        }
        Halt();
    }

    /// <summary>
    /// The <see cref="RingBuffer{T}"/> used by this disruptor.
    /// </summary>
    public RingBuffer<T> RingBuffer => _ringBuffer;

    /// <summary>
    /// Get the value of the cursor indicating the published sequence.
    /// </summary>
    public long Cursor => _ringBuffer.Cursor;

    /// <summary>
    /// The capacity of the data structure to hold entries.
    /// </summary>
    public long BufferSize => _ringBuffer.BufferSize;

    /// <summary>
    /// Get the event for a given sequence in the ring buffer.
    /// </summary>
    /// <param name="sequence">sequence for the event</param>
    /// <returns>event for the sequence</returns>
    public T this[long sequence] => _ringBuffer[sequence];

    /// <summary>
    /// Get the <see cref="DependentSequenceGroup"/> used by a specific handler. Note that the <see cref="DependentSequenceGroup"/>
    /// may be shared by multiple event handlers.
    /// </summary>
    /// <param name="handler">the handler to get the barrier for</param>
    /// <returns>the SequenceBarrier used by the given handler</returns>
    public DependentSequenceGroup? GetDependentSequencesFor(IEventHandler<T> handler) => _consumerRepository.GetDependentSequencesFor(handler);

    /// <summary>
    /// Gets the sequence value for the specified event handlers.
    /// </summary>
    /// <param name="handler">eventHandler to get the sequence for</param>
    /// <returns>eventHandler's sequence</returns>
    public long GetSequenceValueFor(IEventHandler<T> handler) => _consumerRepository.GetSequenceFor(handler).Value;

    /// <summary>
    /// Indicates whether all messages have been consumed by all event processors.
    /// </summary>
    private bool HasBacklog()
    {
        var cursor = _ringBuffer.Cursor;
        return _consumerRepository.HasBacklog(cursor, false);
    }

    /// <summary>
    /// Checks if disruptor has been started.
    /// </summary>
    /// <value>true when start has been called on this instance; otherwise false</value>
    public bool HasStarted => _started == 1;

    internal EventHandlerGroup<T> CreateEventProcessors(Sequence[] barrierSequences, IEventHandler<T>[] eventHandlers)
    {
        CheckNotStarted();

        var processorSequences = new Sequence[eventHandlers.Length];

        for (int i = 0; i < eventHandlers.Length; i++)
        {
            var eventHandler = eventHandlers[i];
            var barrier = _ringBuffer.NewBarrier(eventHandler, barrierSequences);

            var eventProcessor = EventProcessorFactory.Create(_ringBuffer, barrier, eventHandler);

            eventProcessor.SetExceptionHandler(_exceptionHandler);

            _consumerRepository.Add(eventProcessor, eventHandler, barrier.DependentSequences);
            processorSequences[i] = eventProcessor.Sequence;
        }

        UpdateGatingSequencesForNextInChain(barrierSequences, processorSequences);

        return new EventHandlerGroup<T>(this, _consumerRepository, processorSequences);
    }

    internal EventHandlerGroup<T> CreateEventProcessors(Sequence[] barrierSequences, IBatchEventHandler<T>[] eventHandlers)
    {
        CheckNotStarted();

        var processorSequences = new Sequence[eventHandlers.Length];

        for (int i = 0; i < eventHandlers.Length; i++)
        {
            var eventHandler = eventHandlers[i];
            var barrier = _ringBuffer.NewBarrier(eventHandler, barrierSequences);

            var eventProcessor = EventProcessorFactory.Create(_ringBuffer, barrier, eventHandler);

            eventProcessor.SetExceptionHandler(_exceptionHandler);

            _consumerRepository.Add(eventProcessor, eventHandler, barrier.DependentSequences);
            processorSequences[i] = eventProcessor.Sequence;
        }

        UpdateGatingSequencesForNextInChain(barrierSequences, processorSequences);

        return new EventHandlerGroup<T>(this, _consumerRepository, processorSequences);
    }

    internal EventHandlerGroup<T> CreateEventProcessors(Sequence[] barrierSequences, IAsyncBatchEventHandler<T>[] eventHandlers)
    {
        CheckNotStarted();

        var processorSequences = new Sequence[eventHandlers.Length];

        for (int i = 0; i < eventHandlers.Length; i++)
        {
            var eventHandler = eventHandlers[i];

            var sequenceWaiterOwner = SequenceWaiterOwner.EventHandler(eventHandler);
            var barrier = _ringBuffer.NewAsyncBarrier(sequenceWaiterOwner, barrierSequences);
            var eventProcessor = EventProcessorFactory.Create(_ringBuffer, barrier, eventHandler);

            eventProcessor.SetExceptionHandler(_exceptionHandler);

            _consumerRepository.Add(eventProcessor, eventHandler, barrier.DependentSequences);
            processorSequences[i] = eventProcessor.Sequence;
        }

        UpdateGatingSequencesForNextInChain(barrierSequences, processorSequences);

        return new EventHandlerGroup<T>(this, _consumerRepository, processorSequences);
    }

    private void UpdateGatingSequencesForNextInChain(Sequence[] barrierSequences, Sequence[] processorSequences)
    {
        if (processorSequences.Length > 0)
        {
            _ringBuffer.AddGatingSequences(processorSequences);
            foreach (var barrierSequence in barrierSequences)
            {
                _ringBuffer.RemoveGatingSequence(barrierSequence);
            }

            _consumerRepository.UnMarkEventProcessorsAsEndOfChain(barrierSequences);
        }
    }

    internal EventHandlerGroup<T> CreateEventProcessors(Sequence[] barrierSequences, EventProcessorCreator<T>[] processorFactories)
    {
        var eventProcessors = processorFactories.Select(p => CreateEventProcessor(p)).ToArray();

        return HandleEventsWith(eventProcessors);

        IEventProcessor CreateEventProcessor(EventProcessorCreator<T> processorFactory)
        {
            var sequenceBarrier = _ringBuffer.NewBarrier(SequenceWaiterOwner.Unknown, barrierSequences);
            return processorFactory.Invoke(_ringBuffer, sequenceBarrier);
        }
    }

    internal EventHandlerGroup<T> CreateWorkerPool(Sequence[] barrierSequences, IWorkHandler<T>[] workHandlers)
    {
        var workerPool = new WorkerPool<T>(_ringBuffer, barrierSequences, _exceptionHandler, workHandlers);

        _consumerRepository.Add(workerPool, workerPool.DependentSequences);

        var workerSequences = workerPool.GetWorkerSequences();

        UpdateGatingSequencesForNextInChain(barrierSequences, workerSequences);

        return new EventHandlerGroup<T>(this, _consumerRepository, workerSequences);
    }

    private void CheckNotStarted()
    {
        if (_started == 1)
        {
            throw new InvalidOperationException("All event handlers must be added before calling starts.");
        }
    }

    private void CheckOnlyStartedOnce()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException("Disruptor.start() must only be called once.");
        }
    }

    public override string ToString()
    {
        return $"Disruptor {{RingBuffer={_ringBuffer}, Started={_started}}}";
    }
}
