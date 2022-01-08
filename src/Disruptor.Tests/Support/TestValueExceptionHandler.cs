using System;
using System.Collections.Generic;

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

        public List<(Exception exception, long sequence, T data)> EventExceptions { get; } = new();

        public void HandleEventException(Exception ex, long sequence, ref T evt)
        {
            EventExceptions.Add((ex, sequence, evt));

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
