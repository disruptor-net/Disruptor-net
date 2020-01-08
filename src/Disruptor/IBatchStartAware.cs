namespace Disruptor
{
    /// <summary>
    /// Implement this interface in your event handler to be notified when a batch is starting.
    /// </summary>
    public interface IBatchStartAware
    {
        /// <summary>
        /// Called on each batch start before the first call to <see cref="IEventHandler{T}.OnEvent"/> or <see cref="IValueEventHandler{T}.OnEvent"/>.
        /// </summary>
        /// <param name="batchSize">the batch size.</param>
        void OnBatchStart(long batchSize);
    }
}
