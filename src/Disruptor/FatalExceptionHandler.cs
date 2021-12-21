using System;

namespace Disruptor
{
    /// <summary>
    /// Convenience implementation of an exception handler that using standard Console.Writeline to log
    /// the exception re-throw it wrapped in a <see cref="ApplicationException"/>
    /// </summary>
    public sealed class FatalExceptionHandler : IExceptionHandler<object>
    {
        /// <summary>
        /// Strategy for handling uncaught exceptions when processing an event.
        /// </summary>
        /// <param name="ex">exception that propagated from the <see cref="IEventHandler{T}"/>.</param>
        /// <param name="sequence">sequence of the event which cause the exception.</param>
        /// <param name="evt">event being processed when the exception occurred.</param>
        public void HandleEventException(Exception ex, long sequence, object? evt)
        {
            var message = $"Exception processing sequence {sequence} for event {evt}: {ex}";

            Console.WriteLine(message);

            throw new ApplicationException(message, ex);
        }

        /// <summary>
        /// Callback to notify of an exception during <see cref="ILifecycleAware.OnStart"/>
        /// </summary>
        /// <param name="ex">ex throw during the starting process.</param>
        public void HandleOnStartException(Exception ex)
        {
            var message = $"Exception during OnStart(): {ex}";

            Console.WriteLine(message);

            throw new ApplicationException(message, ex);
        }

        /// <summary>
        /// Callback to notify of an exception during <see cref="ILifecycleAware.OnShutdown"/>
        /// </summary>
        /// <param name="ex">ex throw during the shutdown process.</param>
        public void HandleOnShutdownException(Exception ex)
        {
            var message = $"Exception during OnShutdown(): {ex}";

            Console.WriteLine(message);

            throw new ApplicationException(message, ex);
        }
    }
}
