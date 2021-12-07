namespace Disruptor
{
    /// <summary>
    /// Exposes the ring buffer events.
    /// </summary>
    public interface IValueDataProvider<T>
    {
        /// <summary>
        /// Gets the event for a given sequence in the ring buffer.
        /// </summary>
        ref T this[long sequence] { get; }
    }
}
