using System;

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

#if DISRUPTOR_V5
        /// <summary>
        /// Gets a span of events for the given sequences in the RingBuffer.
        /// </summary>
        ReadOnlySpan<T> this[long lo, long hi] { get; }

        EventBatch<T> GetBatch(long lo, long hi);
#endif
    }
}
