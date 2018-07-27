using System;

namespace Disruptor.Tests.Support
{
    public class TestEventHandler<T> : IEventHandler<T>
    {
        private readonly Action<T> _onEventAction;

        public TestEventHandler(Action<T> onEventAction)
        {
            _onEventAction = onEventAction;
        }

        public void OnEvent(T data, long sequence, bool endOfBatch)
        {
            _onEventAction.Invoke(data);
        }
    }
}
