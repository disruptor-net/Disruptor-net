namespace Disruptor.Dsl
{
    /// <summary>
    /// A support class used as part of setting an exception handler for a specific event handler.
    /// For example:
    /// <code>disruptorWizard.HandleExceptionsIn(eventHandler).With(exceptionHandler);</code>
    /// </summary>
    /// <typeparam name="T">the type of event being handled.</typeparam>
    public class ExceptionHandlerSetting<T> where T : class 
    {
        private readonly IEventHandler<T> _eventHandler;
        private readonly ConsumerRepository<T> _consumerRepository;

        internal ExceptionHandlerSetting(IEventHandler<T> eventHandler, ConsumerRepository<T> consumerRepository)
        {
            _eventHandler = eventHandler;
            _consumerRepository = consumerRepository;
        }
        
        /// <summary>
        /// Specify the <see cref="IExceptionHandler{T}"/> to use with the event handler.
        /// </summary>
        /// <param name="exceptionHandler">exceptionHandler the exception handler to use.</param>
        public void With(IExceptionHandler<T> exceptionHandler)
        {
            ((BatchEventProcessor<T>)_consumerRepository.GetEventProcessorFor(_eventHandler)).SetExceptionHandler(exceptionHandler);
            _consumerRepository.GetBarrierFor(_eventHandler).Alert();
        }
    }
}