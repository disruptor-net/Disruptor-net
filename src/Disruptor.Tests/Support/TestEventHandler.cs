using System;

namespace Disruptor.Tests.Support;

public class TestEventHandler<T> : IEventHandler<T>
{
    public TestEventHandler()
    {
    }

    public TestEventHandler(Action<T> onEventAction)
    {
        OnEventAction = onEventAction;
    }

    public Action<T>? OnEventAction { get; }
    public Action? OnTimeoutAction { get; init; }
    public Action? OnStartAction { get; init; }
    public Action? OnShutdownAction { get; init; }

    public void OnEvent(T data, long sequence, bool endOfBatch)
    {
        OnEventAction?.Invoke(data);
    }

    public void OnTimeout(long sequence)
    {
        OnTimeoutAction?.Invoke();
    }

    public void OnStart()
    {
        OnStartAction?.Invoke();
    }

    public void OnShutdown()
    {
        OnShutdownAction?.Invoke();
    }
}
