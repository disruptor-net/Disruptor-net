using System;
using System.Threading;

namespace Disruptor
{
    public class TimeoutBlockingWaitStrategy : IWaitStrategy
    {
        private readonly object _gate = new object();
        private readonly TimeSpan _timeout;

        public TimeoutBlockingWaitStrategy(TimeSpan timeout)
        {
            _timeout = timeout;
        }

        /// <summary>
        /// <see cref="IWaitStrategy.WaitFor"/>
        /// </summary>
        public long WaitFor(long sequence, Sequence cursor, ISequence dependentSequence, CancellationToken cancellationToken)
        {
            var timeSpan = _timeout;
            if (cursor.Value < sequence)
            {
                lock (_gate)
                {
                    while (cursor.Value < sequence)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!Monitor.Wait(_gate, timeSpan))
                        {
                            throw TimeoutException.Instance;
                        }
                    }
                }
            }

            var aggressiveSpinWait = new AggressiveSpinWait();
            long availableSequence;
            while ((availableSequence = dependentSequence.Value) < sequence)
            {
                cancellationToken.ThrowIfCancellationRequested();
                aggressiveSpinWait.SpinOnce();
            }

            return availableSequence;
        }

        /// <summary>
        /// <see cref="IWaitStrategy.SignalAllWhenBlocking"/>
        /// </summary>
        public void SignalAllWhenBlocking()
        {
            lock (_gate)
            {
                Monitor.PulseAll(_gate);
            }
        }
    }
}
