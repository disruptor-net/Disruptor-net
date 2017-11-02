using System;

namespace Disruptor.Tests.Support
{
    public class ActionEventHandler<T> : IEventHandler<T>
    {
        private readonly Action<T> _onEventAction;

        public ActionEventHandler(Action<T> onEventAction)
        {
            _onEventAction = onEventAction;
        }

        public void OnEvent(T data, long sequence, bool endOfBatch)
        {
            _onEventAction.Invoke(data);
        }
    }
}
