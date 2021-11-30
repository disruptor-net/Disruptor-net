namespace Disruptor
{
    /// <summary>
    /// Implement this interface in your event handler to be notified when the wait strategy timeouts.
    /// </summary>
    /// <remarks>
    /// This is only effective in the current wait strategy is a timeout strategy that throws <see cref="TimeoutException"/>
    /// (e.g.: <see cref="TimeoutBlockingWaitStrategy"/>).
    /// </remarks>
    public interface ITimeoutHandler
    {
        /// <summary>
        /// Invoked when the wait strategy throws <see cref="TimeoutException"/>.
        /// </summary>
        /// <param name="sequence"></param>
        void OnTimeout(long sequence);
    }
}
