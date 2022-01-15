using System;

namespace Disruptor.Tests.Support;

public class TestExceptionHandler<T> : IExceptionHandler<T>
    where T : class
{
    private readonly Action<T?> _action;

    public TestExceptionHandler(Action<T?> action)
    {
        _action = action;
    }

    public int EventExceptionCount { get; private set; }

    public void HandleEventException(Exception ex, long sequence, T evt)
    {
        EventExceptionCount++;

        _action.Invoke(evt);
    }

    public int TimeoutExceptionCount { get; private set; }

    public void HandleOnTimeoutException(Exception ex, long sequence)
    {
        TimeoutExceptionCount++;

        _action.Invoke(null);
    }

    public int BatchExceptionCount { get; private set; }

    public void HandleEventException(Exception ex, long sequence, EventBatch<T> batch)
    {
        BatchExceptionCount++;

        foreach (var evt in batch)
        {
            _action.Invoke(evt);
        }
    }

    public void HandleOnStartException(Exception ex)
    {
    }

    public void HandleOnShutdownException(Exception ex)
    {
    }
}