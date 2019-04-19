using System;

namespace Disruptor.Tests.Support
{
    public class TestValueExceptionHandler<T> : IValueExceptionHandler<T>
        where T : struct 
    {
        private readonly Action<T> _action;

        public TestValueExceptionHandler(Action<T> action)
        {
            _action = action;
        }

        public void HandleEventException(Exception ex, long sequence, ref T evt)
        {
            _action.Invoke(evt);
        }

        public void HandleOnTimeoutException(Exception ex, long sequence)
        {
        }

        public void HandleOnStartException(Exception ex)
        {
        }

        public void HandleOnShutdownException(Exception ex)
        {
        }
    }
}