namespace Disruptor.Dsl
{
    /// <summary>
    /// A support class used as part of setting an <see cref="IExceptionHandler"/> for a specific <see cref="IEventHandler{T}"/>.
    /// </summary>
    public class ExceptionHandlerSetting<T> where T : class
    {
        private readonly IEventHandler<T> _eventHandler;
        private readonly ConsumerInfoRepository<T> _consumerInfoRepository;

        internal ExceptionHandlerSetting(IEventHandler<T> eventHandler,
                                       ConsumerInfoRepository<T> consumerInfoRepository)
        {
            _eventHandler = eventHandler;
            _consumerInfoRepository = consumerInfoRepository;
        }

        /// <summary>
        /// Specify the {@link ExceptionHandler} to use with the event handler.
        /// </summary>
        /// <param name="exceptionHandler">the <see cref="IExceptionHandler"/> to use.</param>
        public void With(IExceptionHandler exceptionHandler)
        {
            ((BatchEventProcessor<T>)_consumerInfoRepository.GetEventProcessorFor(_eventHandler)).SetExceptionHandler(exceptionHandler);
            _consumerInfoRepository.GetBarrierFor(_eventHandler).Alert();
        }
    }
}