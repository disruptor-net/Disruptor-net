using Disruptor.Processing;

namespace Disruptor;

/// <summary>
/// Used by the <see cref="IEventProcessor{T}"/> to set a callback allowing the <see cref="IEventHandler{T}"/> to notify
/// when it has finished consuming an event if this happens after the <see cref="IEventHandler{T}.OnEvent"/> call.
/// </summary>
/// <remarks>
/// Typically this would be used when the handler is performing some sort of batching operation such as writing to an IO
/// device; after the operation has completed, the implementation should set <see cref="Sequence.Value"/> to update the
/// sequence and allow other processes that are dependent on this handler to progress.
/// </remarks>
/// <typeparam name="T">event implementation storing the data for sharing during exchange or parallel coordination of an event.</typeparam>
public interface ISequenceReportingEventHandler<in T> : IEventHandler<T>, IEventProcessorSequenceAware
{
}
