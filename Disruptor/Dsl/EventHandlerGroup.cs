using System.Linq;

namespace Disruptor.Dsl
{
    ///<summary>
    ///  A group of <see cref="IEventProcessor"/>s used as part of the <see cref="Disruptor"/>
    ///</summary>
    ///<typeparam name="T">the type of event used by <see cref="IEventProcessor"/>s.</typeparam>
    public class EventHandlerGroup<T> where T : class
    {
        private readonly Disruptor<T> _disruptor;
        private readonly ConsumerRepository<T> _consumerRepository;
        private readonly IEventProcessor[] _eventProcessors;

        internal EventHandlerGroup(Disruptor<T> disruptor, ConsumerRepository<T> consumerRepository, IEventProcessor[] eventProcessors)
        {
            _disruptor = disruptor;
            _consumerRepository = consumerRepository;
            _eventProcessors = eventProcessors;
        }

        /// <summary>
        /// Create a new <see cref="EventHandlerGroup{T}"/> that combines the <see cref="IEventHandler{T}"/> in this group with
        /// input handlers.
        /// </summary>
        /// <param name="handlers">the handlers to combine.</param>
        /// <returns>a new <see cref="EventHandlerGroup{T}"/> combining the existing and new handlers into a single dependency group.</returns>
        public EventHandlerGroup<T> And(params IEventHandler<T>[] handlers)
        {
            var processors = from handler in handlers
                             select _consumerRepository.GetEventProcessorFor(handler);

            var combindedProcessors = _eventProcessors.Concat(processors).ToArray();

            return new EventHandlerGroup<T>(_disruptor, _consumerRepository, combindedProcessors);
        }

        /// <summary>
        /// Create a new <see cref="EventHandlerGroup{T}"/> that combines the handlers in this group with input processors.
        /// </summary>
        /// <param name="processors">the processors to combine.</param>
        /// <returns>a new <see cref="EventHandlerGroup{T}"/> combining the existing and new processors into a single dependency group.</returns>
        public EventHandlerGroup<T> And(params IEventProcessor[] processors)
        {
            var combinedProcessors = processors.Concat(_eventProcessors).ToArray();

            foreach (var eventProcessor in processors)
            {
                _consumerRepository.Add(eventProcessor);
            }

            return new EventHandlerGroup<T>(_disruptor, _consumerRepository, combinedProcessors);
        }

        /// <summary>
        /// Set up batch handlers to consume events from the ring buffer. These handlers will only process events
        /// after every <see cref="IEventProcessor"/> in this group has processed the event.
        /// </summary>
        /// <param name="handlers">the batch handlers that will process events.</param>
        /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to set up an event processor barrier over the created event processors.</returns>
        public EventHandlerGroup<T> Then(params IEventHandler<T>[] handlers)
        {
            return HandleEventsWith(handlers);
        }

        /// <summary>
        /// Set up batch handlers to handle events from the ring buffer. These handlers will only process events
        /// after every <see cref="IEventProcessor"/>s in this group has processed the event.
        /// </summary>
        /// <param name="handlers">the batch handlers that will process events.</param>
        /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to set up a event processor barrier over the created event processors.</returns>
        public EventHandlerGroup<T> HandleEventsWith(params IEventHandler<T>[] handlers)
        {
            return _disruptor.CreateEventProcessors(_eventProcessors, handlers);
        }

        /// <summary>
        /// Create a <see cref="ISequenceBarrier"/> for the processors in this group.
        /// This allows custom event processors to have dependencies on
        /// <see cref="BatchEventProcessor{T}"/>s created by the disruptor.
        /// </summary>
        /// <returns></returns>
        public ISequenceBarrier AsSequenceBarrier()
        {
            return _disruptor.RingBuffer.NewBarrier(Util.GetSequencesFor(_eventProcessors));
        }
    }
}
