using System;

#if DISRUPTOR_V5

namespace Disruptor.Tests.Support
{
    public class TestBatchEventHandler<T> : IBatchEventHandler<T>
    {
        private readonly Action<T> _onEventAction;

        public TestBatchEventHandler(Action<T> onEventAction)
        {
            _onEventAction = onEventAction;
        }

        public void OnBatch(ReadOnlySpan<T> batch, long sequence)
        {
            foreach (var data in batch)
            {
                _onEventAction.Invoke(data);
            }
        }
    }
}

#endif
