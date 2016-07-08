using System;

namespace Disruptor.Dsl
{
    public class ExceptionHandlerWrapper<T> : IExceptionHandler<T> where T : class 
    {
        private IExceptionHandler<T> _handler = new FatalExceptionHandler();

        public void SwitchTo(IExceptionHandler<T> exceptionHandler) => _handler = exceptionHandler;

        public void HandleEventException(Exception ex, long sequence, T evt) => _handler.HandleEventException(ex, sequence, evt);

        public void HandleOnStartException(Exception ex) => _handler.HandleOnStartException(ex);

        public void HandleOnShutdownException(Exception ex) => _handler.HandleOnShutdownException(ex);
    }
}