using System.Collections.Generic;
using System.Linq;
using Disruptor.Processing;

namespace Disruptor.Dsl;

///<summary>
/// A group of <see cref="IEventProcessor"/>s used as part of the <see cref="ValueDisruptor{T}"/>
///</summary>
///<typeparam name="T">the type of event used by <see cref="IEventProcessor"/>s.</typeparam>
public class IpcEventHandlerGroup<T>
    where T : unmanaged
{
    private readonly IpcDisruptor<T> _disruptor;
    private readonly IpcConsumerRepository<T> _consumerRepository;
    private readonly IIpcEventProcessor<T>[] _previousEventProcessors;

    internal IpcEventHandlerGroup(IpcDisruptor<T> disruptor, IpcConsumerRepository<T> consumerRepository, IIpcEventProcessor<T>[] previousEventProcessors)
    {
        _disruptor = disruptor;
        _consumerRepository = consumerRepository;
        _previousEventProcessors = previousEventProcessors;
    }

    /// <summary>
    /// Create a new event handler group that combines the consumers in this group with <paramref name="otherHandlerGroup"/>
    /// </summary>
    /// <param name="otherHandlerGroup">the event handler group to combine</param>
    /// <returns>a new IpcEventHandlerGroup combining the existing and new consumers into a single dependency group</returns>
    public IpcEventHandlerGroup<T> And(IpcEventHandlerGroup<T> otherHandlerGroup)
    {
        return new IpcEventHandlerGroup<T>(_disruptor, _consumerRepository, [.._previousEventProcessors, ..otherHandlerGroup._previousEventProcessors]);
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
    /// <returns>a <see cref="IpcEventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
    public IpcEventHandlerGroup<T> Then(params IValueEventHandler<T>[] handlers) => HandleEventsWith(handlers);

    /// <summary>
    /// Set up batch handlers to handle events from the ring buffer. These handlers will only process events
    /// after every <see cref="IEventProcessor"/> in this group has processed the event.
    ///
    /// This method is generally used as part of a chain. For example if <code>A</code> must
    /// process events before<code> B</code>:
    /// <code>dw.After(A).HandleEventsWith(B);</code>
    /// </summary>
    /// <param name="handlers">the batch handlers that will process events.</param>
    /// <returns>a <see cref="IpcEventHandlerGroup{T}"/> that can be used to set up a event processor barrier over the created event processors.</returns>
    public IpcEventHandlerGroup<T> HandleEventsWith(params IValueEventHandler<T>[] handlers) => _disruptor.CreateEventProcessors(_previousEventProcessors, handlers);
}
