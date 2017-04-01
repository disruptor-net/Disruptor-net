using System;

namespace Disruptor.Tests.Dsl.Stubs
{
    public class TestEventHandler<T> : IEventHandler<T>
    {
        private readonly Action<T, long, bool> _onEventAction;

        public TestEventHandler(Action onEventAction)
            : this((data, sequence, endOfBatch) => onEventAction.Invoke())
        {
        }

        public TestEventHandler(Action<T> onEventAction)
            : this((data, sequence, endOfBatch) => onEventAction.Invoke(data))
        {
        }

        public TestEventHandler(Action<T, long> onEventAction)
            : this((data, sequence, endOfBatch) => onEventAction.Invoke(data, sequence))
        {
        }

        public TestEventHandler(Action<T, long, bool> onEventAction)
        {
            _onEventAction = onEventAction;
        }

        public void OnEvent(T data, long sequence, bool endOfBatch)
        {
            _onEventAction.Invoke(data, sequence, endOfBatch);
        }
    }
}