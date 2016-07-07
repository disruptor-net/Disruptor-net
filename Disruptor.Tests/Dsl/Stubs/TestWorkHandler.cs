using System.Threading;
using Disruptor.Tests.Support;

namespace Disruptor.Tests.Dsl.Stubs
{
    public class TestWorkHandler : IWorkHandler<TestEvent>
    {
        private Volatile.Boolean _readyToProcessEvent = new Volatile.Boolean();
        private volatile bool _stopped;

        public void OnEvent(TestEvent @event)
        {
            WaitForAndSetFlag(false);
        }

        public void ProcessEvent()
        {
            WaitForAndSetFlag(true);
        }

        public void StopWaiting()
        {
            _stopped = true;
        }

        private void WaitForAndSetFlag(bool newValue)
        {
            while (!_stopped && Thread.CurrentThread.IsAlive && !_readyToProcessEvent.AtomicCompareExchange(newValue, !newValue))
            {
                Thread.Yield();
            }
        }
    }
}