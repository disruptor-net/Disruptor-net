using System.Threading;

namespace Disruptor.Tests.Dsl.Stubs
{
    public class EventHandlerStub<T> : IEventHandler<T>, IValueEventHandler<T>
    {
        private readonly CountdownEvent _countDownLatch;

        public EventHandlerStub(CountdownEvent countDownLatch)
        {
            _countDownLatch = countDownLatch;
        }

        public void OnEvent(T data, long sequence, bool endOfBatch)
        {
            _countDownLatch.Signal();
        }

        public void OnEvent(ref T data, long sequence, bool endOfBatch)
        {
            _countDownLatch.Signal();
        }
    }
}
