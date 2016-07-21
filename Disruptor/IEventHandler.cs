namespace Disruptor
{
    /// <summary>
    /// Callback interface to be implemented for processing events as they become available in the <see cref="RingBuffer{T}"/>
    /// </summary>
    /// <typeparam name="T">Type of events for sharing during exchange or parallel coordination of an event.</typeparam>
    /// <remarks>See <see cref="BatchEventProcessor{T}.SetExceptionHandler"/> if you want to handle exceptions propagated out of the handler.</remarks>
    public interface IEventHandler<in T>
    {
        /// <summary>
        /// Called when a publisher has committed an event to the <see cref="RingBuffer{T}"/>
        /// </summary>
        /// <param name="data">Data committed to the <see cref="RingBuffer{T}"/></param>
        /// <param name="sequence">Sequence number committed to the <see cref="RingBuffer{T}"/></param>
        /// <param name="endOfBatch">flag to indicate if this is the last event in a batch from the <see cref="RingBuffer{T}"/></param>
        void OnEvent(T data, long sequence, bool endOfBatch);
    }
}