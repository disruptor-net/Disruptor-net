using System.Threading;
using System.Threading.Tasks;

namespace Disruptor.Tests.Dsl.Stubs;

public class AsyncCountDownEventHandler<T> : IAsyncBatchEventHandler<T>
    where T : class
{
    private readonly CountdownEvent _countDownLatch;

    public AsyncCountDownEventHandler(CountdownEvent countDownLatch)
    {
        _countDownLatch = countDownLatch;
    }

    public ValueTask OnBatch(EventBatch<T> batch, long sequence)
    {
        foreach (var data in batch.AsSpan())
        {
            _countDownLatch.Signal();
        }

        return new ValueTask();
    }
}
