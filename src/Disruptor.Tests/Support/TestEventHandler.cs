using System;

namespace Disruptor.Tests.Support;

public class TestEventHandler<T> : IEventHandler<T>
{
    private readonly Action<T> _onEventAction;
    private readonly Action _onTimeoutAction;

    public TestEventHandler(Action<T> onEventAction)
        : this(onEventAction, () => { })
    {
    }

    public TestEventHandler(Action<T> onEventAction, Action onTimeoutAction)
    {
        _onEventAction = onEventAction;
        _onTimeoutAction = onTimeoutAction;
    }

    public void OnEvent(T data, long sequence, bool endOfBatch)
    {
        _onEventAction.Invoke(data);
    }

    public void OnTimeout(long sequence)
    {
        _onTimeoutAction.Invoke();
    }
}