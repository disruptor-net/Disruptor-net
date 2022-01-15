using System;

namespace Disruptor.Dsl;

public class ValueExceptionHandlerWrapper<T> : IValueExceptionHandler<T> where T : struct 
{
    private IValueExceptionHandler<T> _handler = new ValueFatalExceptionHandler<T>();

    public void SwitchTo(IValueExceptionHandler<T> exceptionHandler) => _handler = exceptionHandler;

    public void HandleEventException(Exception ex, long sequence, ref T evt) => _handler.HandleEventException(ex, sequence, ref evt);

    public void HandleOnTimeoutException(Exception ex, long sequence) => _handler.HandleOnTimeoutException(ex, sequence);

    public void HandleOnStartException(Exception ex) => _handler.HandleOnStartException(ex);

    public void HandleOnShutdownException(Exception ex) => _handler.HandleOnShutdownException(ex);
}