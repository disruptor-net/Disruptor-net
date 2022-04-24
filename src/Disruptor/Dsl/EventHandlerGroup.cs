using System;
using System.Collections.Generic;
using System.Linq;
using Disruptor.Processing;

namespace Disruptor.Dsl;

///<summary>
/// A group of <see cref="IEventProcessor"/>s used as part of the <see cref="Disruptor{T}"/>
///</summary>
///<typeparam name="T">the type of event used by <see cref="IEventProcessor"/>s.</typeparam>
public class EventHandlerGroup<T> where T : class
{
    private readonly Disruptor<T> _disruptor;
    private readonly ConsumerRepository _consumerRepository;
    private readonly ISequence[] _sequences;

    internal EventHandlerGroup(Disruptor<T> disruptor, ConsumerRepository consumerRepository, IEnumerable<ISequence> sequences)
    {
        _disruptor = disruptor;
        _consumerRepository = consumerRepository;
        _sequences = sequences.ToArray();
    }

    /// <summary>
    /// Create a new event handler group that combines the consumers in this group with <paramref name="otherHandlerGroup"/>
    /// </summary>
    /// <param name="otherHandlerGroup">the event handler group to combine</param>
    /// <returns>a new EventHandlerGroup combining the existing and new consumers into a single dependency group</returns>
    public EventHandlerGroup<T> And(EventHandlerGroup<T> otherHandlerGroup)
    {
        return new EventHandlerGroup<T>(_disruptor, _consumerRepository, _sequences.Concat(otherHandlerGroup._sequences));
    }

    /// <summary>
    /// Create a new event handler group that combines the handlers in this group with <paramref name="processors"/>.
    /// </summary>
    /// <param name="processors">the processors to combine</param>
    /// <returns>a new EventHandlerGroup combining the existing and new processors into a single dependency group</returns>
    public EventHandlerGroup<T> And(params IEventProcessor[] processors)
    {
        foreach (var eventProcessor in processors)
        {
            _consumerRepository.Add(eventProcessor);
        }
        return new EventHandlerGroup<T>(_disruptor, _consumerRepository, processors.Select(p => p.Sequence).Concat(_sequences));
    }

    /// <summary>
    /// Set up batch handlers to consume events from the ring buffer. These handlers will only process events
    /// after every <see cref="IEventProcessor"/> in this group has processed the event.
    ///
    /// This method is generally used as part of a chain. For example if the handler <code>A</code> must
    /// process events before handler<code>B</code>: <code>dw.HandleEventsWith(A).Then(B);</code>
    /// </summary>
    /// <param name="handlers"></param>
    /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
    public EventHandlerGroup<T> Then(params IEventHandler<T>[] handlers) => HandleEventsWith(handlers);

    /// <summary>
    /// Set up batch handlers to consume events from the ring buffer. These handlers will only process events
    /// after every <see cref="IEventProcessor"/> in this group has processed the event.
    ///
    /// This method is generally used as part of a chain. For example if the handler <code>A</code> must
    /// process events before handler<code>B</code>: <code>dw.HandleEventsWith(A).Then(B);</code>
    /// </summary>
    /// <param name="handlers"></param>
    /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
    public EventHandlerGroup<T> Then(params IBatchEventHandler<T>[] handlers) => HandleEventsWith(handlers);

    /// <summary>
    /// Set up batch handlers to consume events from the ring buffer. These handlers will only process events
    /// after every <see cref="IEventProcessor"/> in this group has processed the event.
    ///
    /// This method is generally used as part of a chain. For example if the handler <code>A</code> must
    /// process events before handler<code>B</code>: <code>dw.HandleEventsWith(A).Then(B);</code>
    /// </summary>
    /// <param name="handlers"></param>
    /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
    public EventHandlerGroup<T> Then(params IAsyncBatchEventHandler<T>[] handlers) => HandleEventsWith(handlers);

    /// <summary>
    /// Set up custom event processors to handle events from the ring buffer. The Disruptor will
    /// automatically start these processors when started.
    ///
    /// This method is generally used as part of a chain. For example if the handler <code>A</code> must
    /// process events before handler<code>B</code>: <code>dw.HandleEventsWith(A).Then(B);</code>
    /// </summary>
    /// <param name="eventProcessorFactories">the event processor factories to use to create the event processors that will process events.</param>
    /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
    public EventHandlerGroup<T> Then(params IEventProcessorFactory<T>[] eventProcessorFactories) => HandleEventsWith(eventProcessorFactories);

    /// <summary>
    /// Set up a worker pool to handle events from the ring buffer. The worker pool will only process events
    /// after every <see cref="IEventProcessor"/> in this group has processed the event. Each event will be processed
    /// by one of the work handler instances.
    ///
    /// This method is generally used as part of a chain. For example if the handler <code>A</code> must
    /// process events before the worker pool with handlers <code>B, C</code>:
    /// <code>dw.HandleEventsWith(A).ThenHandleEventsWithWorkerPool(B, C);</code>
    /// </summary>
    /// <param name="handlers">the work handlers that will process events. Each work handler instance will provide an extra thread in the worker pool.</param>
    /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to set up a event processor barrier over the created event processors.</returns>
    public EventHandlerGroup<T> ThenHandleEventsWithWorkerPool(params IWorkHandler<T>[] handlers) => HandleEventsWithWorkerPool(handlers);

    /// <summary>
    /// Set up batch handlers to handle events from the ring buffer. These handlers will only process events
    /// after every <see cref="IEventProcessor"/> in this group has processed the event.
    ///
    /// This method is generally used as part of a chain. For example if <code>A</code> must
    /// process events before<code> B</code>:  <code>dw.After(A).HandleEventsWith(B);</code>
    /// </summary>
    /// <param name="handlers">the batch handlers that will process events.</param>
    /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to set up a event processor barrier over the created event processors.</returns>
    public EventHandlerGroup<T> HandleEventsWith(params IEventHandler<T>[] handlers) => _disruptor.CreateEventProcessors(_sequences, handlers);

    /// <summary>
    /// Set up batch handlers to handle events from the ring buffer. These handlers will only process events
    /// after every <see cref="IEventProcessor"/> in this group has processed the event.
    ///
    /// This method is generally used as part of a chain. For example if <code>A</code> must
    /// process events before<code> B</code>:  <code>dw.After(A).HandleEventsWith(B);</code>
    /// </summary>
    /// <param name="handlers">the batch handlers that will process events.</param>
    /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to set up a event processor barrier over the created event processors.</returns>
    public EventHandlerGroup<T> HandleEventsWith(params IBatchEventHandler<T>[] handlers) => _disruptor.CreateEventProcessors(_sequences, handlers);

    /// <summary>
    /// Set up batch handlers to handle events from the ring buffer. These handlers will only process events
    /// after every <see cref="IEventProcessor"/> in this group has processed the event.
    ///
    /// This method is generally used as part of a chain. For example if <code>A</code> must
    /// process events before<code> B</code>:  <code>dw.After(A).HandleEventsWith(B);</code>
    /// </summary>
    /// <param name="handlers">the batch handlers that will process events.</param>
    /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to set up a event processor barrier over the created event processors.</returns>
    public EventHandlerGroup<T> HandleEventsWith(params IAsyncBatchEventHandler<T>[] handlers) => _disruptor.CreateEventProcessors(_sequences, handlers);

    /// <summary>
    /// Set up custom event processors to handle events from the ring buffer. The Disruptor will
    /// automatically start these processors when started.
    ///
    /// This method is generally used as part of a chain. For example if <code>A</code> must
    /// process events before<code> B</code>:  <code>dw.After(A).HandleEventsWith(B);</code>
    /// </summary>
    /// <param name="eventProcessorFactories">eventProcessorFactories the event processor factories to use to create the event processors that will process events.</param>
    /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
    public EventHandlerGroup<T> HandleEventsWith(params IEventProcessorFactory<T>[] eventProcessorFactories) => _disruptor.CreateEventProcessors(_sequences, eventProcessorFactories);

    /// <summary>
    /// Set up a worker pool to handle events from the ring buffer. The worker pool will only process events
    /// after every <see cref="IEventProcessor"/> in this group has processed the event. Each event will be processed
    /// by one of the work handler instances.
    ///
    /// This method is generally used as part of a chain. For example if the handler <code>A</code> must
    /// process events before the worker pool with handlers <code>B, C</code>:
    /// <code>dw.After(A).HandleEventsWithWorkerPool(B, C);</code>
    /// </summary>
    /// <param name="handlers">handlers the work handlers that will process events. Each work handler instance will provide an extra thread in the worker pool.</param>
    /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to set up a event processor barrier over the created event processors.</returns>
    public EventHandlerGroup<T> HandleEventsWithWorkerPool(IWorkHandler<T>[] handlers) => _disruptor.CreateWorkerPool(_sequences, handlers);

    /// <summary>
    /// Create a sequence barrier for the processors in this group.
    /// This allows custom event processors to have dependencies on
    /// <see cref="IEventProcessor{T}"/>s created by the disruptor.
    /// </summary>
    /// <returns>a <see cref="SequenceBarrier"/> including all the processors in this group.</returns>
    public SequenceBarrier AsSequenceBarrier()
    {
        return _disruptor.RingBuffer.NewBarrier(_sequences);
    }

    /// <summary>
    /// Create a sequence barrier for the processors in this group.
    /// This allows custom event processors to have dependencies on
    /// <see cref="IEventProcessor{T}"/>s created by the disruptor.
    /// </summary>
    /// <returns>a <see cref="AsyncSequenceBarrier"/> including all the processors in this group.</returns>
    public AsyncSequenceBarrier AsAsyncSequenceBarrier()
    {
        return _disruptor.RingBuffer.NewAsyncBarrier(_sequences);
    }
}
