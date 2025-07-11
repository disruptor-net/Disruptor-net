using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;
using Disruptor.Util;

namespace Disruptor.Dsl;

/// <summary>
/// Base class for disruptors of value type events.
/// </summary>
/// <typeparam name="T">the type of event used.</typeparam>
/// <seealso cref="ValueDisruptor{T}"/>
/// <seealso cref="UnmanagedDisruptor{T}"/>.
public abstract class ValueTypeDisruptor<T>
    where T : struct
{
    private readonly IValueRingBuffer<T> _ringBuffer;
    private readonly TaskScheduler _taskScheduler;
    private readonly ConsumerRepository _consumerRepository = new();
    private readonly ValueExceptionHandlerWrapper<T> _exceptionHandler = new();
    private readonly DisruptorState _state = new();

    protected ValueTypeDisruptor(IValueRingBuffer<T> ringBuffer, TaskScheduler taskScheduler)
    {
        _ringBuffer = ringBuffer;
        _taskScheduler = taskScheduler;
    }

    public IValueRingBuffer<T> RingBuffer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _ringBuffer;
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
        _state.ThrowIfStarted();
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
    /// <see cref="ValueEventHandlerGroup{T}.HandleEventsWith(ValueEventProcessorCreator{T}[])"/> and <see cref="ValueEventHandlerGroup{T}.Then(ValueEventProcessorCreator{T}[])"/>
    /// which do have barrier sequences to provide.
    ///
    /// This call is additive, but generally should only be called once when setting up the disruptor instance.
    /// </summary>
    /// <param name="eventProcessorFactories">eventProcessorFactories the event processor factories to use to create the event processors that will process events.</param>
    /// <returns>a <see cref="ValueEventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
    public ValueEventHandlerGroup<T> HandleEventsWith(params ValueEventProcessorCreator<T>[] eventProcessorFactories)
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
    /// Indicates whether the disruptor has been started.
    /// </summary>
    public bool HasStarted => _state.HasStarted;

    /// <summary>
    /// Indicates whether the disruptor is running.
    /// </summary>
    public bool IsRunning => _state.IsRunning;

    internal ValueEventHandlerGroup<T> CreateEventProcessors(Sequence[] barrierSequences, IValueEventHandler<T>[] eventHandlers)
    {
        _state.ThrowIfStarted();

        var processorSequences = new Sequence[eventHandlers.Length];

        for (int i = 0; i < eventHandlers.Length; i++)
        {
            var eventHandler = eventHandlers[i];

            var sequenceWaiterOwner = SequenceWaiterOwner.EventHandler(eventHandler);
            var barrier = _ringBuffer.NewBarrier(sequenceWaiterOwner, barrierSequences);
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

    internal ValueEventHandlerGroup<T> CreateEventProcessors(Sequence[] barrierSequences, ValueEventProcessorCreator<T>[] processorFactories)
    {
        var eventProcessors = processorFactories.Select(p => CreateEventProcessor(p)).ToArray();

        return HandleEventsWith(eventProcessors);

        IEventProcessor CreateEventProcessor(ValueEventProcessorCreator<T> processorFactory)
        {
            var sequenceBarrier = _ringBuffer.NewBarrier(SequenceWaiterOwner.Unknown, barrierSequences);
            return processorFactory.Invoke(_ringBuffer, sequenceBarrier);
        }
    }

    public override string ToString()
    {
        return $"ValueTypeDisruptor {{RingBuffer={_ringBuffer}, State={_state}}}";
    }
}
