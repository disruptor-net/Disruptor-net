using System;

namespace Disruptor.Tests.Support;

public class TestValueEventHandler<T> : IValueEventHandler<T>
    where T : struct
{
    public TestValueEventHandler()
        : this(_ => { })
    {
    }

    public TestValueEventHandler(Action<T> onEventAction)
    {
        OnEventAction = onEventAction;
        OnTimeoutAction = () => { };
    }

    public Action<T> OnEventAction { get; set; }
    public Action OnTimeoutAction { get; set; }

    public void OnEvent(ref T data, long sequence, bool endOfBatch)
    {
        OnEventAction.Invoke(data);
    }

    public void OnTimeout(long sequence)
    {
        OnTimeoutAction.Invoke();
    }
}
