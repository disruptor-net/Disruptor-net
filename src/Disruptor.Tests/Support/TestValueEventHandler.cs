using System;

namespace Disruptor.Tests.Support
{
    public class TestValueEventHandler<T> : IValueEventHandler<T>
        where T : struct
    {
        private readonly Action<T> _onEventAction;

        public TestValueEventHandler(Action<T> onEventAction)
        {
            _onEventAction = onEventAction;
        }

        public void OnEvent(ref T data, long sequence, bool endOfBatch)
        {
            _onEventAction.Invoke(data);
        }
    }
}