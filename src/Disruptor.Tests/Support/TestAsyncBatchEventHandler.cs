using System;
using System.Threading.Tasks;

namespace Disruptor.Tests.Support;

public class TestAsyncBatchEventHandler<T> : IAsyncBatchEventHandler<T>
    where T : class
{
    public TestAsyncBatchEventHandler()
        : this(_ => { })
    {
    }

    public TestAsyncBatchEventHandler(Action<T> onEventAction)
    {
        OnEventAction = onEventAction;
        OnTimeoutAction = () => { };
    }

    public Action<T> OnEventAction { get; set; }
    public Action OnTimeoutAction { get; set; }

    public async ValueTask OnBatch(EventBatch<T> batch, long sequence)
    {
        foreach (var data in batch)
        {
            OnEventAction.Invoke(data);
        }

        await Task.Yield();
    }

    public void OnTimeout(long sequence)
    {
        OnTimeoutAction.Invoke();
    }
}
