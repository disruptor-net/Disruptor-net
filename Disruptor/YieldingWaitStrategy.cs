using System;
using System.Diagnostics;
using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// Yielding strategy that uses a Thread.Sleep(0) for <see cref="IEventProcessor"/>s waiting on a barrier
    /// after an initially spinning.
    /// 
    /// This strategy is a good compromise between performance and CPU resource without incurring significant latency spikes.
    /// </summary>
    public sealed class YieldingWaitStrategy : IWaitStrategy
    {
        private const int SpinTries = 100;

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
            long availableSequence;
            var counter = SpinTries;

            if (dependents.Length == 0)
            {
                while ((availableSequence = cursor.Value) < sequence) // volatile read
                {
                    counter = ApplyWaitMethod(barrier, counter);
                }
            }
            else
            {
                while ((availableSequence = Util.GetMinimumSequence(dependents)) < sequence)
                {
                    counter = ApplyWaitMethod(barrier, counter);
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
        public long WaitFor(long sequence, Sequence cursor, Sequence[] dependents, ISequenceBarrier barrier, TimeSpan timeout)
        {
            long availableSequence;
            var counter = SpinTries;
            var sw = Stopwatch.StartNew();

            if (dependents.Length == 0)
            {
                while ((availableSequence = cursor.Value) < sequence) // volatile read
                {
                    counter = ApplyWaitMethod(barrier, counter);
                    if(sw.Elapsed > timeout)
                    {
                        break;
                    }
                }
            }
            else
            {
                while ((availableSequence = Util.GetMinimumSequence(dependents)) < sequence)
                {
                    counter = ApplyWaitMethod(barrier, counter);
                    if (sw.Elapsed > timeout)
                    {
                        break;
                    }
                }
            }

            return availableSequence;
        }

        /// <summary>
        /// Signal those <see cref="IEventProcessor"/> waiting that the cursor has advanced.
        /// </summary>
        public void SignalAllWhenBlocking()
        {
        }

        private static int ApplyWaitMethod(ISequenceBarrier barrier, int counter)
        {
            barrier.CheckAlert();

            if(counter == 0)
            {
                Thread.Sleep(0);
            }
            else
            {
                --counter;
            }

            return counter;
        }
    }
}