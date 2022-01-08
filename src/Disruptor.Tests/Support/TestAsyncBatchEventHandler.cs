using System;
using System.Threading.Tasks;

namespace Disruptor.Tests.Support
{
    public class TestAsyncBatchEventHandler<T> : IAsyncBatchEventHandler<T>, ITimeoutHandler
        where T : class
    {
        private readonly Action<T> _onEventAction;
        private readonly Action _onTimeoutAction;

        public TestAsyncBatchEventHandler(Action<T> onEventAction)
            : this(onEventAction, () => { })
        {
        }

        public TestAsyncBatchEventHandler(Action<T> onEventAction, Action onTimeoutAction)
        {
            _onEventAction = onEventAction;
            _onTimeoutAction = onTimeoutAction;
        }

        public async ValueTask OnBatch(EventBatch<T> batch, long sequence)
        {
            foreach (var data in batch)
            {
                _onEventAction.Invoke(data);
            }

            await Task.Yield();
        }

        public void OnTimeout(long sequence)
        {
            _onTimeoutAction.Invoke();
        }
    }
}
