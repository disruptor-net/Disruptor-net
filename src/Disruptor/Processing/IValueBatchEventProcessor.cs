using System;

namespace Disruptor.Processing
{
    /// <summary>
    /// Interface for <see cref="ValueBatchEventProcessor{T,TDataProvider,TSequenceBarrier,TEventHandler,TBatchStartAware}"/>.
    /// </summary>
    /// <typeparam name="T">the type of event used.</typeparam>
    public interface IValueBatchEventProcessor<T> : IEventProcessor
        where T : struct 
    {
        /// <summary>
        /// Waits before the event processor enters the <see cref="IEventProcessor.IsRunning"/> state.
        /// </summary>
        /// <param name="timeout">maximum wait duration</param>
        void WaitUntilStarted(TimeSpan timeout);

        /// <summary>
        /// Set a new <see cref="IValueExceptionHandler{T}"/> for handling exceptions propagated out of the <see cref="IEventHandler{T}"/>
        /// </summary>
        /// <param name="exceptionHandler">exceptionHandler to replace the existing exceptionHandler.</param>
        void SetExceptionHandler(IValueExceptionHandler<T> exceptionHandler);
    }
}
