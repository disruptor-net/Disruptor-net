using System.Threading;
using Disruptor.Tests.Support;

namespace Disruptor.Tests.Dsl.Stubs
{
    public class TestWorkHandler : IWorkHandler<TestEvent>
    {
        private int _readyToProcessEvent;
        private volatile bool _stopped;

        public void OnEvent(TestEvent @event)
        {
            WaitForAndSetFlag(0);
        }

        public void ProcessEvent()
        {
            WaitForAndSetFlag(1);
        }

        public void StopWaiting()
        {
            _stopped = true;
        }

        private void WaitForAndSetFlag(int newValue)
        {
            while (!_stopped && Thread.CurrentThread.IsAlive && Interlocked.Exchange(ref _readyToProcessEvent, newValue)  == newValue)
            {
                Thread.Yield();
            }
        }
    }
}