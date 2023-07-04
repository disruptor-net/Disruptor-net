using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;

namespace Disruptor.Dsl;

/// <summary>
/// Base class for disruptors of value type events.
/// </summary>
/// <typeparam name="T">the type of event used.</typeparam>
/// <typeparam name="TRingBuffer">the type of the underlying ring buffer.</typeparam>
/// <seealso cref="ValueDisruptor{T}"/>
/// <seealso cref="UnmanagedDisruptor{T}"/>.
public abstract class ValueDisruptor<T, TRingBuffer> : IValueDisruptor<T>
    where T : struct
    where TRingBuffer : IValueRingBuffer<T>
{
    protected readonly TRingBuffer _ringBuffer;
    private readonly TaskScheduler _taskScheduler;
    private readonly ConsumerRepository _consumerRepository = new();
    private readonly ValueExceptionHandlerWrapper<T> _exceptionHandler = new();
    private volatile int _started;

    protected ValueDisruptor(TRingBuffer ringBuffer, TaskScheduler taskScheduler)
    {
        _ringBuffer = ringBuffer;
        _taskScheduler = taskScheduler;
    }

    IValueRingBuffer<T> IValueDisruptor<T>.RingBuffer => _ringBuffer;

    /// <summary>
    /// Set up event handlers to handle events from the ring buffer. These handlers will process events
    /// as soon as they become available, in parallel.
    ///
    /// <code>dw.HandleEventsWith(A).Then(B);</code>
    ///
    /// This call is additive, but generally should only be called once when setting up the disruptor instance.
    /// </summary>
    /// <param name="handlers">the event handlers that will process events</param>
    /// <returns>a <see cref="ValueEventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
    public ValueEventHandlerGroup<T> HandleEventsWith(params IValueEventHandler<T>[] handlers)
    {
        return CreateEventProcessors(Array.Empty<Sequence>(), handlers);
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
    /// <returns>a <see cref="ValueEventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
    public ValueEventHandlerGroup<T> HandleEventsWith(params IEventProcessor[] processors)
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

        return new ValueEventHandlerGroup<T>(this, _consumerRepository, DisruptorUtil.GetSequencesFor(processors));
    }

    /// <summary>
    /// Specify an exception handler to be used for event handlers and worker pools created by this disruptor.
    /// The exception handler will be used by existing and future event handlers and worker pools created by this disruptor instance.
    /// </summary>
    /// <param name="exceptionHandler">the exception handler to use</param>
    public void SetDefaultExceptionHandler(IValueExceptionHandler<T> exceptionHandler)
    {
        CheckNotStarted();
        _exceptionHandler.SwitchTo(exceptionHandler);
    }

    /// <summary>
    /// Override the default exception handler for a specific handler.
    /// <code>disruptorWizard.HandleExceptionsIn(eventHandler).With(exceptionHandler);</code>
    /// </summary>
    /// <param name="eventHandler">eventHandler the event handler to set a different exception handler for</param>
    /// <returns>an <see cref="ValueExceptionHandlerSetting{T}"/> dsl object - intended to be used by chaining the with method call</returns>
    public ValueExceptionHandlerSetting<T> HandleExceptionsFor(IValueEventHandler<T> eventHandler)
    {
        return new ValueExceptionHandlerSetting<T>(eventHandler, _consumerRepository);
    }

    /// <summary>
    /// Create a group of event handlers to be used as a dependency.
    /// For example if the handler <code>A</code> must process events before handler <code>B</code>:
    /// <code>dw.After(A).HandleEventsWith(B);</code>
    /// </summary>
    /// <param name="handlers">handlers the event handlers, previously set up with <see cref="HandleEventsWith(IValueEventHandler{T}[])"/>,
    /// that will form the barrier for subsequent handlers or processors.</param>
    /// <returns>an <see cref="ValueEventHandlerGroup{T}"/> that can be used to setup a dependency barrier over the specified event handlers.</returns>
    public ValueEventHandlerGroup<T> After(params IValueEventHandler<T>[] handlers)
    {
        return new ValueEventHandlerGroup<T>(this, _consumerRepository, handlers.Select(h => _consumerRepository.GetSequenceFor(h)));
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
    /// <see cref="ValueEventHandlerGroup{T}.HandleEventsWith(IValueEventProcessorFactory{T}[])"/> and <see cref="ValueEventHandlerGroup{T}.Then(IValueEventProcessorFactory{T}[])"/>
    /// which do have barrier sequences to provide.
    ///
    /// This call is additive, but generally should only be called once when setting up the disruptor instance.
    /// </summary>
    /// <param name="eventProcessorFactories">eventProcessorFactories the event processor factories to use to create the event processors that will process events.</param>
    /// <returns>a <see cref="ValueEventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
    public ValueEventHandlerGroup<T> HandleEventsWith(params IValueEventProcessorFactory<T>[] eventProcessorFactories)
    {
        return CreateEventProcessors(Array.Empty<Sequence>(), eventProcessorFactories);
    }

    /// <summary>
    /// Create a group of event processors to be used as a dependency.
    /// </summary>
    /// <param name="processors">processors the event processors, previously set up with <see cref="HandleEventsWith(IValueEventHandler{T}[])"/>,
    /// that will form the barrier for subsequent handlers or processors.</param>
    /// <returns>an <see cref="ValueEventHandlerGroup{T}"/> that can be used to setup a <see cref="SequenceBarrier"/> over the specified event processors.</returns>
    /// <seealso cref="After(IValueEventHandler{T}[])"/>
    public ValueEventHandlerGroup<T> After(params IEventProcessor[] processors)
    {
        return new ValueEventHandlerGroup<T>(this, _consumerRepository, DisruptorUtil.GetSequencesFor(processors));
    }

    /// <summary>
    /// Starts the event processors and returns the fully configured ring buffer.
    ///
    /// The ring buffer is set up to prevent overwriting any entry that is yet to
    /// be processed by the slowest event processor.
    ///
    /// This method must only be called once after all event processors have been added.
    /// </summary>
    /// <returns>the configured ring buffer</returns>
    public TRingBuffer Start()
    {
        CheckOnlyStartedOnce();
        foreach (var consumerInfo in _consumerRepository)
        {
            consumerInfo.Start(_taskScheduler);
        }

        return _ringBuffer;
    }

    /// <summary>
    /// Calls <see cref="IEventProcessor.Halt"/> on all of the event processors created via this disruptor.
    /// </summary>
    public void Halt()
    {
        foreach (var consumerInfo in _consumerRepository)
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
    /// Get the <see cref="DependentSequenceGroup"/> used by a specific handler. Note that the <see cref="DependentSequenceGroup"/>
    /// may be shared by multiple event handlers.
    /// </summary>
    /// <param name="handler">the handler to get the barrier for</param>
    /// <returns>the SequenceBarrier used by the given handler</returns>
    public DependentSequenceGroup? GetDependentSequencesFor(IValueEventHandler<T> handler) => _consumerRepository.GetDependentSequencesFor(handler);

    /// <summary>
    /// Gets the sequence value for the specified event handlers.
    /// </summary>
    /// <param name="handler">eventHandler to get the sequence for</param>
    /// <returns>eventHandler's sequence</returns>
    public long GetSequenceValueFor(IValueEventHandler<T> handler) => _consumerRepository.GetSequenceFor(handler).Value;

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
    /// Checks if disruptor has been started.
    /// </summary>
    /// <value>true when start has been called on this instance; otherwise false</value>
    public bool HasStarted => _started == 1;

    ValueEventHandlerGroup<T> IValueDisruptor<T>.CreateEventProcessors(Sequence[] barrierSequences, IValueEventHandler<T>[] eventHandlers)
    {
        return CreateEventProcessors(barrierSequences, eventHandlers);
    }

    private ValueEventHandlerGroup<T> CreateEventProcessors(Sequence[] barrierSequences, IValueEventHandler<T>[] eventHandlers)
    {
        CheckNotStarted();

        var processorSequences = new Sequence[eventHandlers.Length];
        var barrier = _ringBuffer.NewBarrier(barrierSequences);

        for (int i = 0; i < eventHandlers.Length; i++)
        {
            var eventHandler = eventHandlers[i];

            var eventProcessor = EventProcessorFactory.Create(_ringBuffer, barrier, eventHandler);

            eventProcessor.SetExceptionHandler(_exceptionHandler);

            _consumerRepository.Add(eventProcessor, eventHandler, barrier.DependentSequences);
            processorSequences[i] = eventProcessor.Sequence;
        }

        UpdateGatingSequencesForNextInChain(barrierSequences, processorSequences);

        return new ValueEventHandlerGroup<T>(this, _consumerRepository, processorSequences);
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

    ValueEventHandlerGroup<T> IValueDisruptor<T>.CreateEventProcessors(Sequence[] barrierSequences, IValueEventProcessorFactory<T>[] processorFactories)
    {
        return CreateEventProcessors(barrierSequences, processorFactories);
    }

    private ValueEventHandlerGroup<T> CreateEventProcessors(Sequence[] barrierSequences, IValueEventProcessorFactory<T>[] processorFactories)
    {
        var barrier = _ringBuffer.NewBarrier(barrierSequences);
        var eventProcessors = processorFactories.Select(p => p.CreateEventProcessor(_ringBuffer, barrier)).ToArray();

        return HandleEventsWith(eventProcessors);
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
            throw new InvalidOperationException("ValueDisruptor.start() must only be called once.");
        }
    }

    public override string ToString()
    {
        return $"ValueDisruptor {{RingBuffer={_ringBuffer}, Started={_started}}}";
    }
}
