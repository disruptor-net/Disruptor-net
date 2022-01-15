using System.Collections.Generic;
using System.Linq;
using Disruptor.Processing;

namespace Disruptor.Dsl;

///<summary>
/// A group of <see cref="IEventProcessor"/>s used as part of the <see cref="ValueDisruptor{T}"/>
///</summary>
///<typeparam name="T">the type of event used by <see cref="IEventProcessor"/>s.</typeparam>
public class ValueEventHandlerGroup<T>
    where T : struct
{
    private readonly IValueDisruptor<T> _disruptor;
    private readonly ConsumerRepository _consumerRepository;
    private readonly ISequence[] _sequences;

    internal ValueEventHandlerGroup(IValueDisruptor<T> disruptor, ConsumerRepository consumerRepository, IEnumerable<ISequence> sequences)
    {
        _disruptor = disruptor;
        _consumerRepository = consumerRepository;
        _sequences = sequences.ToArray();
    }

    /// <summary>
    /// Create a new event handler group that combines the consumers in this group with <paramref name="otherHandlerGroup"/>
    /// </summary>
    /// <param name="otherHandlerGroup">the event handler group to combine</param>
    /// <returns>a new ValueEventHandlerGroup combining the existing and new consumers into a single dependency group</returns>
    public ValueEventHandlerGroup<T> And(ValueEventHandlerGroup<T> otherHandlerGroup)
    {
        return new ValueEventHandlerGroup<T>(_disruptor, _consumerRepository, _sequences.Concat(otherHandlerGroup._sequences));
    }

    /// <summary>
    /// Create a new event handler group that combines the handlers in this group with <paramref name="processors"/>.
    /// </summary>
    /// <param name="processors">the processors to combine</param>
    /// <returns>a new ValueEventHandlerGroup combining the existing and new processors into a single dependency group</returns>
    public ValueEventHandlerGroup<T> And(params IEventProcessor[] processors)
    {
        foreach (var eventProcessor in processors)
        {
            _consumerRepository.Add(eventProcessor);
        }
        return new ValueEventHandlerGroup<T>(_disruptor, _consumerRepository, processors.Select(p => p.Sequence).Concat(_sequences));
    }

    /// <summary>
    /// Set up batch handlers to consume events from the ring buffer. These handlers will only process events
    /// after every <see cref="IEventProcessor"/> in this group has processed the event.
    ///
    /// This method is generally used as part of a chain. For example if the handler <code>A</code> must
    /// process events before handler<code>B</code>:
    /// <code>dw.HandleEventsWith(A).then(B);</code>
    /// </summary>
    /// <param name="handlers"></param>
    /// <returns>a <see cref="ValueEventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
    public ValueEventHandlerGroup<T> Then(params IValueEventHandler<T>[] handlers) => HandleEventsWith(handlers);

    /// <summary>
    /// Set up custom event processors to handle events from the ring buffer. The Disruptor will
    /// automatically start these processors when started.
    ///
    /// This method is generally used as part of a chain. For example if the handler <code>A</code> must
    /// process events before handler<code>B</code>:
    /// </summary>
    /// <param name="eventProcessorFactories">the event processor factories to use to create the event processors that will process events.</param>
    /// <returns>a <see cref="ValueEventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
    public ValueEventHandlerGroup<T> Then(params IValueEventProcessorFactory<T>[] eventProcessorFactories) => HandleEventsWith(eventProcessorFactories);

    /// <summary>
    /// Set up batch handlers to handle events from the ring buffer. These handlers will only process events
    /// after every <see cref="IEventProcessor"/> in this group has processed the event.
    ///
    /// This method is generally used as part of a chain. For example if <code>A</code> must
    /// process events before<code> B</code>:
    /// <code>dw.After(A).HandleEventsWith(B);</code>
    /// </summary>
    /// <param name="handlers">the batch handlers that will process events.</param>
    /// <returns>a <see cref="ValueEventHandlerGroup{T}"/> that can be used to set up a event processor barrier over the created event processors.</returns>
    public ValueEventHandlerGroup<T> HandleEventsWith(params IValueEventHandler<T>[] handlers) => _disruptor.CreateEventProcessors(_sequences, handlers);

    /// <summary>
    /// Set up custom event processors to handle events from the ring buffer. The Disruptor will
    /// automatically start these processors when started.
    ///
    /// This method is generally used as part of a chain. For example if <code>A</code> must
    /// process events before<code> B</code>:
    /// <code>dw.After(A).HandleEventsWith(B);</code>
    /// </summary>
    /// <param name="eventProcessorFactories">eventProcessorFactories the event processor factories to use to create the event processors that will process events.</param>
    /// <returns>a <see cref="ValueEventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
    public ValueEventHandlerGroup<T> HandleEventsWith(params IValueEventProcessorFactory<T>[] eventProcessorFactories) => _disruptor.CreateEventProcessors(_sequences, eventProcessorFactories);

    /// <summary>
    /// Create a dependency barrier for the processors in this group.
    /// This allows custom event processors to have dependencies on
    /// <see cref="IValueEventProcessor{T}"/>s created by the disruptor.
    /// </summary>
    /// <returns>a <see cref="ISequenceBarrier"/> including all the processors in this group.</returns>
    public ISequenceBarrier AsSequenceBarrier() => _disruptor.RingBuffer.NewBarrier(_sequences);
}