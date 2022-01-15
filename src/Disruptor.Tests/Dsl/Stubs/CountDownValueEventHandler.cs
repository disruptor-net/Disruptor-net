using System.Threading;

namespace Disruptor.Tests.Dsl.Stubs;

public class CountDownValueEventHandler<T> : IValueEventHandler<T>
    where T : struct
{
    private readonly CountdownEvent _countDownLatch;

    public CountDownValueEventHandler(CountdownEvent countDownLatch)
    {
        _countDownLatch = countDownLatch;
    }

    public void OnEvent(ref T data, long sequence, bool endOfBatch)
    {
        _countDownLatch.Signal();
    }
}