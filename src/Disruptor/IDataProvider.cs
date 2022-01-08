namespace Disruptor
{
    /// <summary>
    /// Exposes the ring buffer events.
    /// </summary>
    public interface IDataProvider<T>
        where T : class
    {
        /// <summary>
        /// Gets the event for a given sequence in the ring buffer.
        /// </summary>
        T this[long sequence] { get; }

        /// <summary>
        /// Gets a batch of events for the given sequences in the RingBuffer.
        /// </summary>
        /// <remarks>
        /// Because the ring buffer is a circular data structure, it is possible that the [lo, hi] sequence interval
        /// does not reference a contiguous portion of the underlying array. In this case the returned batch will contain
        /// the largest possible contiguous array segment that starts from <paramref name="lo"/>.
        ///
        /// Please never assume that the returned batch contains all the events.
        /// Always check the returned batch length.
        /// </remarks>
        /// <param name="lo">the lowest sequence number</param>
        /// <param name="hi">the highest sequence number</param>
        EventBatch<T> GetBatch(long lo, long hi);
    }
}
