using System;
using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// Blocking strategy that uses a lock and condition variable for <see cref="IEventProcessor"/>s waiting on a barrier.
    /// 
    /// This strategy should be used when performance and low-latency are not as important as CPU resource.
    /// </summary>
    public sealed class BlockingWaitStrategy : IWaitStrategy
    {
        private readonly object _gate = new object();
        private volatile int _numWaiters;

        /// <summary>
        /// Wait for the given sequence to be available
        /// </summary>
        /// <param name="sequence">sequence to be waited on.</param>
        /// <param name="cursor">Ring buffer cursor on which to wait.</param>
        /// <param name="dependents">dependents further back the chain that must advance first</param>
        /// <param name="barrier">barrier the <see cref="IEventProcessor"/> is waiting on.</param>
        /// <returns>the sequence that is available which may be greater than the requested sequence.</returns>
        public long WaitFor(long sequence, Sequence cursor, Sequence[] dependents, ISequenceBarrier barrier)
        {
            var availableSequence = cursor.Value; // volatile read
            if (availableSequence < sequence)
            {
                Monitor.Enter(_gate);
                try
                {
                    ++_numWaiters;
                    while ((availableSequence = cursor.Value) < sequence) // volatile read
                    {
                        barrier.CheckAlert();
                        Monitor.Wait(_gate);
                    }
                }
                finally
                {
                    --_numWaiters;
                    Monitor.Exit(_gate);
                }
            }

            if (dependents.Length != 0)
            {
                while ((availableSequence = Util.GetMinimumSequence(dependents)) < sequence)
                {
                    barrier.CheckAlert();
                }
            }

            return availableSequence;
        }

        /// <summary>
        /// Wait for the given sequence to be available with a timeout specified.
        /// </summary>
        /// <param name="sequence">sequence to be waited on.</param>
        /// <param name="cursor">cursor on which to wait.</param>
        /// <param name="dependents">dependents further back the chain that must advance first</param>
        /// <param name="barrier">barrier the processor is waiting on.</param>
        /// <param name="timeout">timeout value to abort after.</param>
        /// <returns>the sequence that is available which may be greater than the requested sequence.</returns>
        /// <exception cref="AlertException">AlertException if the status of the Disruptor has changed.</exception>
        public long WaitFor(long sequence, Sequence cursor, Sequence[] dependents, ISequenceBarrier barrier,
                        TimeSpan timeout)
        {
            long availableSequence;
            if ((availableSequence = cursor.Value) < sequence)
            {
                Monitor.Enter(_gate);
                try
                {
                    ++_numWaiters;
                    while ((availableSequence = cursor.Value) < sequence)
                    {
                        barrier.CheckAlert();

                        if(!Monitor.Wait(_gate, timeout))
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    --_numWaiters;
                    Monitor.Exit(_gate);
                }
            }

            if (dependents.Length != 0)
            {
                while ((availableSequence = Util.GetMinimumSequence(dependents)) < sequence)
                {
                    barrier.CheckAlert();
                }
            }

            return availableSequence;
        }

        /// <summary>
        /// Signal those <see cref="IEventProcessor"/> waiting that the cursor has advanced.
        /// </summary>
        public void SignalAllWhenBlocking()
        {
            if(_numWaiters != 0)
            {
                lock(_gate)
                {
                    Monitor.PulseAll(_gate);
                }
            }
        }
    }
}