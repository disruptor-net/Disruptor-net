using System;

namespace Disruptor
{
    /// <summary>
    /// Convenience implementation of an exception handler that using Console.WriteLine to log
    /// the exception
    /// </summary>
    public class IgnoreExceptionHandler : IExceptionHandler
    {
        /// <summary>
        /// Strategy for handling uncaught exceptions when processing an event.
        /// </summary>
        /// <param name="ex">exception that propagated from the <see cref="IEventHandler{T}"/>.</param>
        /// <param name="sequence">sequence of the event which cause the exception.</param>
        /// <param name="evt">event being processed when the exception occurred.</param>
        public void HandleEventException(Exception ex, long sequence, object evt)
        {
            var message = string.Format("Exception processing sequence {0} for event {1}: {2}", sequence, evt, ex);

            Console.WriteLine(message);
        }

        /// <summary>
        /// Callback to notify of an exception during <see cref="ILifecycleAware.OnStart"/>
        /// </summary>
        /// <param name="ex">ex throw during the starting process.</param>
        public void HandleOnStartException(Exception ex)
        {
            var message = string.Format("Exception during OnStart(): {0}", ex);

            Console.WriteLine(message);
        }

        /// <summary>
        /// Callback to notify of an exception during <see cref="ILifecycleAware.OnShutdown"/>
        /// </summary>
        /// <param name="ex">ex throw during the shutdown process.</param>
        public void HandleOnShutdownException(Exception ex)
        {
            var message = string.Format("Exception during OnShutdown(): {0}", ex);

            Console.WriteLine(message);
        }
    }
}