using System;

namespace Disruptor.Tests.Support;

public class TestBatchEventHandler<T> : IBatchEventHandler<T>
    where T : class
{
    public TestBatchEventHandler()
        : this(_ => { })
    {
    }

    public TestBatchEventHandler(Action<T> onEventAction)
    {
        OnEventAction = onEventAction;
        OnTimeoutAction = () => { };
    }

    public Action<T> OnEventAction { get; set; }
    public Action OnTimeoutAction { get; set; }

    public void OnBatch(EventBatch<T> batch, long sequence)
    {
        foreach (var data in batch)
        {
            OnEventAction.Invoke(data);
        }
    }

    public void OnTimeout(long sequence)
    {
        OnTimeoutAction.Invoke();
    }
}
