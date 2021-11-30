using System;
using System.Threading;
using Disruptor.Processing;

namespace Disruptor
{
    /// <summary>
    /// Blocking strategy that uses a lock and condition variable for <see cref="IEventProcessor"/> waiting on a barrier.
    /// However it will periodically wake up if it has been idle for specified period by returning <see cref="SequenceWaitResult.Timeout"/>.
    /// To make use of this, the event handler class should implement the <see cref="ITimeoutHandler"/>.
    /// </summary>
    /// <remarks>
    /// This strategy can be used when throughput and low-latency are not as important as CPU resource.
    /// </remarks>
    public class TimeoutBlockingWaitStrategy : IWaitStrategy
    {
        private readonly object _gate = new object();
        private readonly TimeSpan _timeout;

        public TimeoutBlockingWaitStrategy(TimeSpan timeout)
        {
            _timeout = timeout;
        }

        public SequenceWaitResult WaitFor(long sequence, Sequence cursor, ISequence dependentSequence, CancellationToken cancellationToken)
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
                            return SequenceWaitResult.Timeout;
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

        public void SignalAllWhenBlocking()
        {
            lock (_gate)
            {
                Monitor.PulseAll(_gate);
            }
        }
    }
}
