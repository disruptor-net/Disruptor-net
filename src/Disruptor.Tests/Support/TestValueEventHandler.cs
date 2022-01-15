using System;

namespace Disruptor.Tests.Support
{
    public class TestValueEventHandler<T> : IValueEventHandler<T>
        where T : struct
    {
        private readonly Action<T> _onEventAction;
        private readonly Action _onTimeoutAction;

        public TestValueEventHandler(Action<T> onEventAction)
            : this(onEventAction, () => { })
        {
        }

        public TestValueEventHandler(Action<T> onEventAction, Action onTimeoutAction)
        {
            _onEventAction = onEventAction;
            _onTimeoutAction = onTimeoutAction;
        }

        public void OnEvent(ref T data, long sequence, bool endOfBatch)
        {
            _onEventAction.Invoke(data);
        }

        public void OnTimeout(long sequence)
        {
            _onTimeoutAction.Invoke();
        }
    }
}
