using System;

namespace Disruptor.Dsl
{
    public class ExceptionHandlerWrapper<T> : IExceptionHandler<T> where T : class 
    {
        private readonly FatalExceptionHandler _defaultHandler = new FatalExceptionHandler();
        private IExceptionHandler<T> _switchedHandler;

        public void SwitchTo(IExceptionHandler<T> exceptionHandler) => _switchedHandler = exceptionHandler;

        public void HandleEventException(Exception ex, long sequence, T evt)
        {
            if (_switchedHandler != null)
            {
                _switchedHandler.HandleEventException(ex, sequence, evt);
                return;
            }

            _defaultHandler.HandleEventException(ex, sequence, evt);
        }

        public void HandleOnStartException(Exception ex)
        {
            if (_switchedHandler != null)
            {
                _switchedHandler.HandleOnStartException(ex);
                return;
            }

            _defaultHandler.HandleOnStartException(ex);
        }

        public void HandleOnShutdownException(Exception ex)
        {
            if (_switchedHandler != null)
            {
                _switchedHandler.HandleOnShutdownException(ex);
                return;
            }

            _defaultHandler.HandleOnShutdownException(ex);
        }
    }
}