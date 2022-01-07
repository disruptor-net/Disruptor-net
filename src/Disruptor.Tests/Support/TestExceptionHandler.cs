using System;

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

        public void HandleEventException(Exception ex, long sequence, T? evt)
        {
            _action.Invoke(evt);
        }

#if DISRUPTOR_V5
        public void HandleEventException(Exception ex, long sequence, EventBatch<T> batch)
        {
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
