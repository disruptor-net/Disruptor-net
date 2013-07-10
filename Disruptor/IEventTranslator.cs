namespace Disruptor
{
    /// <summary>
    /// Implementations translate another data representations into events claimed from the <see cref="RingBuffer{T}"/>
    /// </summary>
    /// <typeparam name="T">event implementation storing the data for sharing during exchange or parallel coordination of an event.</typeparam>
    public interface IEventTranslator<T>
    {
        /// <summary>
        /// Translate a data representation into fields set in given event
        /// </summary>
        /// <param name="eventData">event into which the data should be translated.</param>
        /// <param name="sequence">sequence that is assigned to event.</param>        
        void TranslateTo(T eventData, long sequence);
    }
}