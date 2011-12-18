namespace Disruptor.Dsl
{
    /// <summary>
    /// A support class used as part of setting an <see cref="IExceptionHandler"/> for a specific <see cref="IEventHandler{T}"/>.
    /// </summary>
    public class ExceptionHandlerSetting<T> where T : class
    {
        private readonly IEventHandler<T> _eventHandler;
        private readonly EventProcessorRepository<T> _eventProcessorRepository;

        internal ExceptionHandlerSetting(IEventHandler<T> eventHandler,
                                       EventProcessorRepository<T> eventProcessorRepository)
        {
            _eventHandler = eventHandler;
            _eventProcessorRepository = eventProcessorRepository;
        }

        /// <summary>
        /// Specify the {@link ExceptionHandler} to use with the event handler.
        /// </summary>
        /// <param name="exceptionHandler">the <see cref="IExceptionHandler"/> to use.</param>
        public void With(IExceptionHandler exceptionHandler)
        {
            ((BatchEventProcessor<T>)_eventProcessorRepository.GetEventProcessorFor(_eventHandler)).SetExceptionHandler(exceptionHandler);
            _eventProcessorRepository.GetBarrierFor(_eventHandler).Alert();
        }
    }
}