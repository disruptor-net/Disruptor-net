using System;
using System.Threading;

namespace Disruptor
{
    internal class RunningFlag
    {
        private const int Stopped = 0;
        private const int Running = 1;

        private volatile int _isRunning;

        public bool IsRunning => Thread.VolatileRead(ref _isRunning) == Running;

        public void MarkAsStopped()
        {
            Thread.VolatileWrite(ref _isRunning, Stopped);
        }

        public void MarkAsRunning(string exceptionMessage = null)
        {
            if (!TryMarkAsRunning())
                throw new InvalidOperationException(string.IsNullOrEmpty(exceptionMessage) ? "Thread is already running" : exceptionMessage);
        }

        public bool TryMarkAsRunning()
        {
            return Interlocked.Exchange(ref _isRunning, Running) == Stopped;
        }
    }
}