namespace Disruptor
{
    /// <summary>
    /// An aggregate collection of <see cref="IEventHandler{T}"/> that get called in sequence for each event.
    /// </summary>
    public class AggregateEventHandler<T> : IEventHandler<T>, ILifecycleAware
    {
        private readonly IEventHandler<T>[] _eventHandlers;

        /// <summary>
        /// Construct an aggregate collection of <see cref="IEventHandler{T}"/> to be called in sequence.
        /// </summary>
        /// <param name="eventHandlers"></param>
        public AggregateEventHandler(params IEventHandler<T>[] eventHandlers)
        {
            _eventHandlers = eventHandlers;
        }

        /// <summary>
        /// Called when a publisher has committed an event to the <see cref="RingBuffer{T}"/>
        /// </summary>
        /// <param name="data">Data committed to the <see cref="RingBuffer{T}"/></param>
        /// <param name="sequence">Sequence number committed to the <see cref="RingBuffer{T}"/></param>
        /// <param name="endOfBatch">flag to indicate if this is the last event in a batch from the <see cref="RingBuffer{T}"/></param>
        public void OnNext(T data, long sequence, bool endOfBatch)
        {
            for (int i = 0; i < _eventHandlers.Length; i++)
            {
                var eventHandler = _eventHandlers[i];
                eventHandler.OnNext(data, sequence, endOfBatch);
            }
        }

        ///<summary>
        /// Called once on thread start before first event is available.
        ///</summary>
        public void OnStart()
        {
            foreach (var eventHandler in _eventHandlers)
            {
                var lifecycleAware = eventHandler as ILifecycleAware;
                if(lifecycleAware != null)
                {
                    lifecycleAware.OnStart();                    
                }
            }
        }

        /// <summary>
        /// Called once just before the thread is shutdown.
        /// </summary>
        public void OnShutdown()
        {
            foreach (var eventHandler in _eventHandlers)
            {
                var lifecycleAware = eventHandler as ILifecycleAware;
                if(lifecycleAware != null)
                {
                    lifecycleAware.OnShutdown();                    
                }
            }
        }
    }
}
