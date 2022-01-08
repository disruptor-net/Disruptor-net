using System;
using System.Collections.Generic;

namespace Disruptor.Tests.Support
{
    public class TestExceptionHandler<T> : IExceptionHandler<T>
        where T : class
    {
        private readonly Action<T?> _action;

        public TestExceptionHandler(Action<T?> action)
        {
            _action = action;
        }

        public List<(Exception exception, long sequence, T? data)> EventExceptions { get; } = new();

        public void HandleEventException(Exception ex, long sequence, T? evt)
        {
            EventExceptions.Add((ex, sequence, evt));

            _action.Invoke(evt);
        }

#if DISRUPTOR_V5
        public List<(Exception exception, long sequence, EventBatch<T> batch)> BatchExceptions { get; } = new();

        public void HandleEventException(Exception ex, long sequence, EventBatch<T> batch)
        {
            BatchExceptions.Add((ex, sequence, batch));

            foreach (var evt in batch)
            {
                _action.Invoke(evt);
            }
        }
#endif

        public void HandleOnStartException(Exception ex)
        {
        }

        public void HandleOnShutdownException(Exception ex)
        {
        }
    }
}
