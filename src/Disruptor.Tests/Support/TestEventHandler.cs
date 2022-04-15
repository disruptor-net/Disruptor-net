using System;

namespace Disruptor.Tests.Support;

public class TestEventHandler<T> : IEventHandler<T>
{
    public TestEventHandler()
        : this(_ => { })
    {
    }

    public TestEventHandler(Action<T> onEventAction)
    {
        OnEventAction = onEventAction;
        OnTimeoutAction = () => { };
    }

    public Action<T> OnEventAction { get; set; }
    public Action OnTimeoutAction { get; set; }

    public void OnEvent(T data, long sequence, bool endOfBatch)
    {
        OnEventAction.Invoke(data);
    }

    public void OnTimeout(long sequence)
    {
        OnTimeoutAction.Invoke();
    }
}
