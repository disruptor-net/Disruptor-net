using System;

namespace Disruptor.Tests.Support;

public class TestValueEventHandler<T> : IValueEventHandler<T>
    where T : struct
{
    public TestValueEventHandler()
    {
    }

    public TestValueEventHandler(Action<T> onEventAction)
    {
        OnEventAction = onEventAction;
    }

    public Action<T>? OnEventAction { get; }
    public Action? OnTimeoutAction { get; init; }
    public Action? OnStartAction { get; init; }
    public Action? OnShutdownAction { get; init; }

    public void OnEvent(ref T data, long sequence, bool endOfBatch)
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
