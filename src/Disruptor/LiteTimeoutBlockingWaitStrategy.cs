using System;
using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// Variation of the <see cref="TimeoutBlockingWaitStrategy"/> that attempts to elide conditional wake-ups
    /// when the lock is uncontended.
    /// </summary>
    public class LiteTimeoutBlockingWaitStrategy : IWaitStrategy
    {
        private readonly object _lock = new object();
        private volatile int _signalNeeded;
        private readonly int _timeoutInMilliseconds;

        /// <summary>
        /// Creates a <see cref="LiteTimeoutBlockingWaitStrategy"/> with the specified timeout.
        /// </summary>
        public LiteTimeoutBlockingWaitStrategy(TimeSpan timeout)
        {
            _timeoutInMilliseconds = (int)timeout.TotalMilliseconds;
        }

        /// <summary>
        /// <see cref="IWaitStrategy.WaitFor"/>.
        /// </summary>
        public long WaitFor(long sequence, Sequence cursor, ISequence dependentSequence, ISequenceBarrier barrier)
        {
            var milliseconds = _timeoutInMilliseconds;

            long availableSequence;
            if (cursor.Value < sequence)
            {
                lock (_lock)
                {
                    while (cursor.Value < sequence)
                    {
                        Interlocked.Exchange(ref _signalNeeded, 1);

                        barrier.CheckAlert();
                        
                         if (!Monitor.Wait(_lock, milliseconds))
                        {
                            throw TimeoutException.Instance;
                        }
                    }
                }
            }

            while ((availableSequence = dependentSequence.Value) < sequence)
            {
                barrier.CheckAlert();
            }

            return availableSequence;
        }

        /// <summary>
        /// <see cref="IWaitStrategy.SignalAllWhenBlocking"/>.
        /// </summary>
        public void SignalAllWhenBlocking()
        {
            if (Interlocked.Exchange(ref _signalNeeded, 0) == 1)
            {
                lock (_lock)
                {
                    Monitor.PulseAll(_lock);
                }
            }
        }
    }
}