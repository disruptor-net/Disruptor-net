using System;

namespace Disruptor
{
    /// <summary>
    /// Exposes the ring buffer events.
    /// </summary>
    public interface IDataProvider<T>
    {
        /// <summary>
        /// Gets the event for a given sequence in the ring buffer.
        /// </summary>
        T this[long sequence] { get; }

#if NETCOREAPP
        /// <summary>
        /// Gets a span of events for the given sequences in the RingBuffer.
        /// </summary>
        ReadOnlySpan<T> this[long lo, long hi] { get; }
#endif
    }
}
