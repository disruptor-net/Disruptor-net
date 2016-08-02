namespace Disruptor
{
    /// <summary>
    /// Callback interface to be implemented for processing units of work as they become available in the <see cref="RingBuffer{T}"/>
    /// 
    /// </summary>
    /// <typeparam name="T">event implementation storing the data for sharing during exchange or parallel coordination of an event.</typeparam>
    public interface IWorkHandler<in T>
    {
        /// <summary>
        /// Callback to indicate a unit of work needs to be processed.
        /// </summary>
        /// <param name="evt">event published to the <see cref="RingBuffer{T}"/></param>
        void OnEvent(T evt);   
    }
}