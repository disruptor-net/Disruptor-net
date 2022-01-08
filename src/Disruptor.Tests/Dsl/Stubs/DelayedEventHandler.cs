using System;
using System.Threading;
using Disruptor.Tests.Support;

namespace Disruptor.Tests.Dsl.Stubs
{
    public class DelayedEventHandler : IEventHandler<TestEvent>, IValueEventHandler<TestValueEvent>
    {
        private int _readyToProcessEvent;
        private volatile bool _stopped;
        private readonly Barrier _barrier;

        private DelayedEventHandler(Barrier barrier)
        {
            _barrier = barrier;
        }

        public DelayedEventHandler() : this(new Barrier(2))
        {
        }

        public void OnEvent(TestEvent entry, long sequence, bool endOfBatch)
        {
            WaitForAndSetFlag(0);
        }

        public void OnEvent(ref TestValueEvent data, long sequence, bool endOfBatch)
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
            while (!_stopped && Thread.CurrentThread.IsAlive && Interlocked.Exchange(ref _readyToProcessEvent, newValue) == newValue)
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
