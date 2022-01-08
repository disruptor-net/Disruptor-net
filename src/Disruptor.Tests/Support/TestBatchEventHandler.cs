using System;

#if DISRUPTOR_V5

namespace Disruptor.Tests.Support
{
    public class TestBatchEventHandler<T> : IBatchEventHandler<T>, ITimeoutHandler
        where T : class
    {
        private readonly Action<T> _onEventAction;
        private readonly Action _onTimeoutAction;

        public TestBatchEventHandler(Action<T> onEventAction)
            : this(onEventAction, () => { })
        {
        }

        public TestBatchEventHandler(Action<T> onEventAction, Action onTimeoutAction)
        {
            _onEventAction = onEventAction;
            _onTimeoutAction = onTimeoutAction;
        }

        public void OnBatch(EventBatch<T> batch, long sequence)
        {
            foreach (var data in batch)
            {
                _onEventAction.Invoke(data);
            }
        }

        public void OnTimeout(long sequence)
        {
            _onTimeoutAction.Invoke();
        }
    }
}

#endif
