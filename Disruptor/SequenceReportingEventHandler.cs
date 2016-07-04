namespace Disruptor
{
    /// <summary>
    /// Used by the <see cref="BatchEventProcessor{T}"/> to set a callback allowing the<see cref="IEventHandler{T}"/> to notify
    /// when it has finished consuming an event if this happens after the <see cref="IEventHandler{T}.OnEvent"/> call.
    /// </summary>
    /// <typeparam name="T">event implementation storing the data for sharing during exchange or parallel coordination of an event.</typeparam>
    public interface ISequenceReportingEventHandler<in T> : IEventHandler<T>
    {
        /// <summary>
        /// Call by the <see cref="BatchEventProcessor{T}"/> to setup the callback.
        /// </summary>
        /// <param name="sequenceCallback">callback on which to notify the <see cref="BatchEventProcessor{T}"/> that the sequence has progressed.</param>
       void SetSequenceCallback(Sequence sequenceCallback);
    }
}