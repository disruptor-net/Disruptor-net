using System;

namespace Disruptor
{
    /// <summary>
    /// Convenience implementation of an exception handler that using Console.WriteLine to log the exception
    /// </summary>
    public class IgnoreExceptionHandler<T> : IExceptionHandler<T>
        where T : class
    {
        /// <summary>
        /// Strategy for handling uncaught exceptions when processing an event.
        /// </summary>
        /// <param name="ex">exception that propagated from the <see cref="IEventHandler{T}"/>.</param>
        /// <param name="sequence">sequence of the event which cause the exception.</param>
        /// <param name="evt">event being processed when the exception occurred.</param>
        public void HandleEventException(Exception ex, long sequence, T? evt)
        {
            var message = $"Exception processing sequence {sequence} for event {evt}: {ex}";

            Console.WriteLine(message);
        }

#if DISRUPTOR_V5
        public void HandleEventException(Exception ex, long sequence, EventBatch<T> batch)
        {
            var message = $"Exception processing sequence {sequence} for batch of {batch.Length} events, first event {batch[0]}: {ex}";

            Console.WriteLine(message);
        }
#endif

        /// <summary>
        /// Callback to notify of an exception during <see cref="ILifecycleAware.OnStart"/>
        /// </summary>
        /// <param name="ex">ex throw during the starting process.</param>
        public void HandleOnStartException(Exception ex)
        {
            var message = $"Exception during OnStart(): {ex}";

            Console.WriteLine(message);
        }

        /// <summary>
        /// Callback to notify of an exception during <see cref="ILifecycleAware.OnShutdown"/>
        /// </summary>
        /// <param name="ex">ex throw during the shutdown process.</param>
        public void HandleOnShutdownException(Exception ex)
        {
            var message = $"Exception during OnShutdown(): {ex}";

            Console.WriteLine(message);
        }
    }
}
