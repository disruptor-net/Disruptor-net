using System.Threading;
using Disruptor.Tests.Support;

namespace Disruptor.Tests.Dsl
{
    internal class SleepingEventHandler : IEventHandler<TestEvent> 
    {
        public void OnNext(TestEvent entry, long sequence, bool endOfBatch)
        {
            Thread.Sleep(1000);
        }
    }
}