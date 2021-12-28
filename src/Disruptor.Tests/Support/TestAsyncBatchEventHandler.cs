using System;
using System.Threading.Tasks;

#if DISRUPTOR_V5

namespace Disruptor.Tests.Support
{
    public class TestAsyncBatchEventHandler<T> : IAsyncBatchEventHandler<T>
        where T : class
    {
        private readonly Action<T> _onEventAction;
        private readonly bool _yield;

        public TestAsyncBatchEventHandler(Action<T> onEventAction, bool yield = false)
        {
            _onEventAction = onEventAction;
            _yield = yield;
        }

        public async ValueTask OnBatch(EventBatch<T> batch)
        {
            foreach (var data in batch)
            {
                _onEventAction.Invoke(data);
            }

            if (_yield)
                await Task.Yield();
        }
    }
}

#endif
