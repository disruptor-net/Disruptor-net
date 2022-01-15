using System;
using System.Collections.Generic;

namespace Disruptor.Tests.Support
{
    public class TestValueExceptionHandler<T> : IValueExceptionHandler<T>
        where T : struct
    {
        private readonly Action<T?> _action;

        public TestValueExceptionHandler(Action<T?> action)
        {
            _action = action;
        }

        public int EventExceptionCount { get; private set; }

        public void HandleEventException(Exception ex, long sequence, ref T evt)
        {
            EventExceptionCount++;

            _action.Invoke(evt);
        }

        public int TimeoutExceptionCount { get; private set; }

        public void HandleOnTimeoutException(Exception ex, long sequence)
        {
            TimeoutExceptionCount++;

            _action.Invoke(null);
        }

        public void HandleOnStartException(Exception ex)
        {
        }

        public void HandleOnShutdownException(Exception ex)
        {
        }
    }
}
