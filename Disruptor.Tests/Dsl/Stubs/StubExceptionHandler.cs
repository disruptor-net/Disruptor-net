using System;
using System.Threading;

namespace Disruptor.Tests.Dsl.Stubs
{
    public class StubExceptionHandler : IExceptionHandler<object>
    {
        private Volatile.Reference<Exception> _exceptionHandled;

        public StubExceptionHandler(Volatile.Reference<Exception> exceptionHandled)
        {
            this._exceptionHandled = exceptionHandled;
        }

        public void HandleEventException(Exception ex, long sequence, Object @event)
        {
            _exceptionHandled.WriteFullFence(ex);
        }

        public void HandleOnStartException(Exception ex)
        {
            _exceptionHandled.WriteFullFence(ex);
        }

        public void HandleOnShutdownException(Exception ex)
        {
            _exceptionHandled.WriteFullFence(ex);
        }
    }
}