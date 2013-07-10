using System.Threading;
using Disruptor.Tests.Support;

namespace Disruptor.Tests.Dsl
{
    internal class EventHandlerStub : IEventHandler<TestEvent>
    {
        private readonly CountdownEvent _countDownEvent;

        public EventHandlerStub(CountdownEvent countDownEvent)
        {
            _countDownEvent = countDownEvent;
        }

        public void OnNext(TestEvent data, long sequence, bool endOfBatch)
        {
            _countDownEvent.Signal();
        }
    }
}