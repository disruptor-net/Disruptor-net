using System.Threading;
using Disruptor.Tests.Support;

namespace Disruptor.Tests.Dsl
{
    internal class DelayedEventHandler : IEventHandler<TestEvent>
    {
        private Volatile.Boolean readyToProcessEvent = new Volatile.Boolean(false);
        private volatile bool stopped = false;

        public void OnNext(TestEvent data, long sequence, bool endOfBatch)
        {
            WaitForAndSetFlag(false);
        }

        public void ProcessEvent()
        {
            WaitForAndSetFlag(true);
        }

        public void StopWaiting()
        {
            stopped = true;
        }


        private void WaitForAndSetFlag(bool newValue)
        {
            while (!stopped && !readyToProcessEvent.AtomicCompareExchange(newValue, !newValue)) {
                try {
                    Thread.Yield();
                }
                catch (ThreadInterruptedException e) {
                    return;
                }
            }
        }
    }
}