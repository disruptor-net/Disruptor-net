using System.Threading;

namespace Disruptor.Tests.Dsl.Stubs
{
    public class CountDownEventHandler<T> : IEventHandler<T>
    {
        private readonly CountdownEvent _countDownLatch;

        public CountDownEventHandler(CountdownEvent countDownLatch)
        {
            _countDownLatch = countDownLatch;
        }

        public void OnEvent(T data, long sequence, bool endOfBatch)
        {
            _countDownLatch.Signal();
        }
    }
}
