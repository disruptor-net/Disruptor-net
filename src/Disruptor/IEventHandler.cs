namespace Disruptor;

/// <summary>
/// Marker interface for event handler interfaces.
/// </summary>
/// <remarks>
/// See: <see cref="IEventHandler{T}"/>, <see cref="IValueEventHandler{T}"/>, <see cref="IBatchEventHandler{T}"/>, <see cref="IAsyncBatchEventHandler{T}"/>.
/// </remarks>
public interface IEventHandler
{
    /// <summary>
    /// Limits the size of event batches.
    /// </summary>
    /// <remarks>
    /// The value will be read only once on start, thus dynamically changing the max batch size is not supported.
    /// </remarks>
    int? MaxBatchSize => null;

    ///<summary>
    /// Called once on thread start before first event is available.
    ///</summary>
    void OnStart()
    {
    }

    /// <summary>
    /// Called once just before the thread is shutdown.
    /// </summary>
    /// <remarks>
    /// Sequence event processing will already have stopped before this method is called. No events will
    /// be processed after this message.
    /// </remarks>
    void OnShutdown()
    {
    }

    /// <summary>
    /// Invoked when the wait strategy timeouts.
    /// </summary>
    /// <remarks>
    /// This only happens if the current wait strategy can return timeouts (e.g.: <see cref="TimeoutBlockingWaitStrategy"/>).
    /// </remarks>
    void OnTimeout(long sequence)
    {
    }
}
