using System;

namespace Disruptor.Tests.Support;

public class TestExceptionHandler<T> : IExceptionHandler<T>
    where T : class
{
    private readonly Action<(T? data, Exception ex)> _action;

    public TestExceptionHandler(Action<(T? data, Exception ex)> action)
    {
        _action = action;
    }

    public int EventExceptionCount { get; private set; }

    public void HandleEventException(Exception ex, long sequence, T evt)
    {
        EventExceptionCount++;

        _action.Invoke((evt, ex));
    }

    public int TimeoutExceptionCount { get; private set; }

    public void HandleOnTimeoutException(Exception ex, long sequence)
    {
        TimeoutExceptionCount++;

        _action.Invoke((null, ex));
    }

    public int BatchExceptionCount { get; private set; }

    public void HandleEventException(Exception ex, long sequence, EventBatch<T> batch)
    {
        BatchExceptionCount++;

        foreach (var evt in batch)
        {
            _action.Invoke((evt, ex));
        }
    }

    public void HandleOnStartException(Exception ex)
    {
    }

    public void HandleOnShutdownException(Exception ex)
    {
    }
}
