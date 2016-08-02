using System.Threading;
using Disruptor.Tests.Support;

namespace Disruptor.Tests.Dsl.Stubs
{
    public class SleepingEventHandler : IEventHandler<TestEvent>
    {
        public void OnEvent(TestEvent data, long sequence, bool endOfBatch)
        {
            Thread.Sleep(1000);
        }
    }
}