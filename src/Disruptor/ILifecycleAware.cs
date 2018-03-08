namespace Disruptor
{
    /// <summary>
    /// Implement this interface in your <see cref="IEventHandler{T}"/> to be notified when a thread for the
    /// <see cref="IBatchEventProcessor{T}"/> starts and shuts down.
    /// </summary>
    public interface ILifecycleAware
    {
        ///<summary>
        /// Called once on thread start before first event is available.
        ///</summary>
        void OnStart();

        /// <summary>
        /// Called once just before the thread is shutdown.
        /// 
        /// Sequence event processing will already have stopped before this method is called. No events will
        /// be processed after this message.
        /// </summary>
        void OnShutdown();
    }
}
