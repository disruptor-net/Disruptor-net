using System;

namespace Disruptor
{
    /// <summary>
    /// Callback handler for uncaught exceptions in the event processing cycle of the <see cref="BatchEventProcessor{T}"/>
    /// </summary>
    public interface IExceptionHandler<in T>
    {
        /// <summary>
        /// Strategy for handling uncaught exceptions when processing an event.
        /// 
        /// If the strategy wishes to terminate further processing by the <see cref="BatchEventProcessor{T}"/>
        /// then it should throw a <see cref="ApplicationException"/>
        /// </summary>
        /// <param name="ex">exception that propagated from the <see cref="IEventHandler{T}"/>.</param>
        /// <param name="sequence">sequence of the event which cause the exception.</param>
        /// <param name="evt">event being processed when the exception occurred. This can be null</param>
        void HandleEventException(Exception ex, long sequence, T evt);

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