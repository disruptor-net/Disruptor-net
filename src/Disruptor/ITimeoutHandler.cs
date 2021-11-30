namespace Disruptor
{
    /// <summary>
    /// Implement this interface in your event handler to be notified when the wait strategy timeouts.
    /// </summary>
    /// <remarks>
    /// This is only effective if the current wait strategy can return timeouts (e.g.: <see cref="TimeoutBlockingWaitStrategy"/>).
    /// </remarks>
    public interface ITimeoutHandler
    {
        /// <summary>
        /// Invoked when the wait strategy timeouts.
        /// </summary>
        void OnTimeout(long sequence);
    }
}
