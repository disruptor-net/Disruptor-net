using Disruptor.Processing;

namespace Disruptor;

/// <summary>
/// Callback interface to be implemented for processing events as they become available in the <see cref="RingBuffer{T}"/>
/// </summary>
/// <remarks>
/// <para>
/// This interface is an alternative to <see cref="IEventHandler{T}"/>. It is very slightly slower than <see cref="IEventHandler{T}"/>
/// for batches of size one, but faster for larger batches.
/// </para>
/// <para>
/// Consider using this type if you need to process events in batches.
/// </para>
/// <para>
/// Please note that the batches can be very large, the worst case being the size of your ring buffer. It might be appropriate to process
/// the batch in bounded-size chunks in some use cases (e.g.: database persistence).
/// </para>
/// </remarks>
/// <typeparam name="T">Type of events for sharing during exchange or parallel coordination of an event.</typeparam>
public interface IBatchEventHandler<T> : IEventHandler
    where T : class
{
    /// <summary>
    /// Called when a publisher has committed events to the <see cref="RingBuffer{T}"/>. The <see cref="IEventProcessor{T}"/> will
    /// read messages from the <see cref="RingBuffer{T}"/> in batches, where a batch is all of the events available to be
    /// processed without having to wait for any new event to arrive. This can be useful for event handlers that need
    /// to do slower operations like I/O as they can group together the data from multiple events into a single operation.
    /// </summary>
    /// <param name="batch">Batch of events committed to the <see cref="RingBuffer{T}"/></param>
    /// <param name="sequence">Sequence number of the first event of the batch</param>
    void OnBatch(EventBatch<T> batch, long sequence);
}
