using System;
using Disruptor.Tests.Support;

namespace Disruptor.Tests.Dsl.Stubs
{
    public class StubExceptionHandler : IExceptionHandler<TestEvent>, IValueExceptionHandler<TestValueEvent>
    {
        private readonly AtomicReference<Exception> _exceptionHandled;

        public StubExceptionHandler(AtomicReference<Exception> exceptionHandled)
        {
            _exceptionHandled = exceptionHandled;
        }

        public void HandleEventException(Exception ex, long sequence, TestEvent? evt)
        {
            _exceptionHandled.Write(ex);
        }

#if DISRUPTOR_V5
        public void HandleEventException(Exception ex, long sequence, EventBatch<TestEvent> batch)
        {
            _exceptionHandled.Write(ex);
        }
#endif

        public void HandleEventException(Exception ex, long sequence, ref TestValueEvent evt)
        {
            _exceptionHandled.Write(ex);
        }

        public void HandleOnTimeoutException(Exception ex, long sequence)
        {
            _exceptionHandled.Write(ex);
        }

        public void HandleOnStartException(Exception ex)
        {
            _exceptionHandled.Write(ex);
        }

        public void HandleOnShutdownException(Exception ex)
        {
            _exceptionHandled.Write(ex);
        }
    }
}
