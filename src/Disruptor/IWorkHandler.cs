namespace Disruptor;

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

    ///<summary>
    /// Called once on thread start before first event is available.
    ///</summary>
    void OnStart()
    {
    }

    /// <summary>
    /// Called once just before the thread is shutdown.
    ///
    /// Sequence event processing will already have stopped before this method is called. No events will
    /// be processed after this message.
    /// </summary>
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