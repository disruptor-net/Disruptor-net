using System;
using System.Threading;
using Disruptor.Tests.Support;

namespace Disruptor.Tests.Dsl.Stubs
{
    public class DelayedEventHandler : IEventHandler<TestEvent>, ILifecycleAware
    {
        private Volatile.Boolean _readyToProcessEvent = new Volatile.Boolean();
        private volatile bool _stopped;
        private readonly Barrier _barrier;

        public DelayedEventHandler(Barrier barrier)
        {
            _barrier = barrier;
        }

        public DelayedEventHandler() : this(new Barrier(2))
        {
        }

        public void OnEvent(TestEvent entry, long sequence, bool endOfBatch)
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
            while (!_stopped && Thread.CurrentThread.IsAlive &&
                !_readyToProcessEvent.AtomicCompareExchange(newValue, !newValue))
            {
                Thread.Yield();
            }
        }

        public void OnStart()
        {
            try
            {
                _barrier.SignalAndWait();
            }
            catch (ThreadInterruptedException e)
            {
                throw new ApplicationException("", e);
            }
            catch (BarrierPostPhaseException ex)
            {
                throw new ApplicationException("", ex);
            }
        }

        public void OnShutdown()
        {
        }

        public void AwaitStart()
        {
            _barrier.SignalAndWait();
        }
    }
}