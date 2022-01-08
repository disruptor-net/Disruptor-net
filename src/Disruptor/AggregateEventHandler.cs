namespace Disruptor
{
    /// <summary>
    /// An aggregate collection of <see cref="IEventHandler{T}"/> that get called in sequence for each event.
    /// </summary>
    /// <typeparam name="T">event implementation storing the data for sharing during exchange or parallel coordination of an event</typeparam>
    public class AggregateEventHandler<T> : IEventHandler<T>
    {
        private readonly IEventHandler<T>[] _eventHandlers;

        /// <summary>
        /// Construct an aggregate collection of <see cref="IEventHandler{T}"/> to be called in sequence.
        /// </summary>
        /// <param name="eventHandlers">to be called in sequence</param>
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
        public void OnEvent(T data, long sequence, bool endOfBatch)
        {
            // for loop instead of foreach in order to avoid bound checks, we're here in the critical path
            for (var i = 0; i < _eventHandlers.Length; i++)
            {
                _eventHandlers[i].OnEvent(data, sequence, endOfBatch);
            }
        }

        ///<summary>
        /// Called once on thread start before first event is available.
        ///</summary>
        public void OnStart()
        {
            foreach (var eventHandler in _eventHandlers)
            {
                eventHandler.OnStart();
            }
        }

        /// <summary>
        /// Called once just before the thread is shutdown.
        /// </summary>
        public void OnShutdown()
        {
            foreach (var eventHandler in _eventHandlers)
            {
                eventHandler.OnShutdown();
            }
        }
    }
}
