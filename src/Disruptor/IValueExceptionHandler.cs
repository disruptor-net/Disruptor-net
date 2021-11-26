using System;
using Disruptor.Processing;

namespace Disruptor
{
    /// <summary>
    /// Callback handler for uncaught exceptions in the event processing cycle of the <see cref="IValueBatchEventProcessor{T}"/>
    /// </summary>
    public interface IValueExceptionHandler<T> where T : struct
    {
        /// <summary>
        /// Strategy for handling uncaught exceptions when processing an event.
        /// 
        /// If the strategy wishes to terminate further processing by the <see cref="IValueBatchEventProcessor{T}"/>
        /// then it should throw a <see cref="ApplicationException"/>
        /// </summary>
        /// <param name="ex">exception that propagated from the <see cref="IValueEventHandler{T}"/>.</param>
        /// <param name="sequence">sequence of the event which cause the exception.</param>
        /// <param name="evt">event being processed when the exception occurred. This can be null</param>
        void HandleEventException(Exception ex, long sequence, ref T evt);

        /// <summary>
        /// Callback to notify of an exception during <see cref="ITimeoutHandler.OnTimeout"/>
        /// </summary>
        /// <param name="ex">ex throw during the starting process.</param>
        /// <param name="sequence">sequence of the event which cause the exception.</param>
        void HandleOnTimeoutException(Exception ex, long sequence);

        /// <summary>
        /// Callback to notify of an exception during <see cref="ILifecycleAware.OnStart"/>
        /// </summary>
        /// <param name="ex">ex throw during the starting process.</param>
        void HandleOnStartException(Exception ex);

        /// <summary>
        /// Callback to notify of an exception during <see cref="ILifecycleAware.OnShutdown"/>
        /// </summary>
        /// <param name="ex">ex throw during the shutdown process.</param>
        void HandleOnShutdownException(Exception ex);   
    }
}
