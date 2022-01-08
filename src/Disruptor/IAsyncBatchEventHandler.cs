using System.Threading.Tasks;

namespace Disruptor
{
    public interface IAsyncBatchEventHandler<T> where T : class
    {
        ValueTask OnBatch(EventBatch<T> batch, long sequence);

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
}
