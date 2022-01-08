using System;

namespace Disruptor.Dsl
{
    public class ExceptionHandlerWrapper<T> : IExceptionHandler<T>
        where T : class
    {
        private IExceptionHandler<T> _handler = new FatalExceptionHandler<T>();

        public void SwitchTo(IExceptionHandler<T> exceptionHandler) => _handler = exceptionHandler;

        public void HandleEventException(Exception ex, long sequence, T evt) => _handler.HandleEventException(ex, sequence, evt);

        public void HandleOnTimeoutException(Exception ex, long sequence) => _handler.HandleOnTimeoutException(ex, sequence);

#if DISRUPTOR_V5
        public void HandleEventException(Exception ex, long sequence, EventBatch<T> batch) => _handler.HandleEventException(ex, sequence, batch);
#endif

        public void HandleOnStartException(Exception ex) => _handler.HandleOnStartException(ex);

        public void HandleOnShutdownException(Exception ex) => _handler.HandleOnShutdownException(ex);
    }
}
