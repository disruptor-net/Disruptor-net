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
        /// <para>This strategy is a good compromise between performance and CPU resource without incurring significant latency spikes.</para>
        /// </summary>
        /// <param name="sequence">sequence to be waited on.</param>
        /// <param name="cursor">Ring buffer cursor on which to wait.</param>
        /// <param name="dependentSequence">dependents further back the chain that must advance first</param>
        /// <param name="barrier">barrier the <see cref="IEventProcessor"/> is waiting on.</param>
        /// <returns>the sequence that is available which may be greater than the requested sequence.</returns>
        public long WaitFor(long sequence, Sequence cursor, ISequence dependentSequence, ISequenceBarrier barrier)
        {
            long availableSequence;
            var counter = SpinTries;

            while ((availableSequence = dependentSequence.Value) < sequence)
            {
                counter = ApplyWaitMethod(barrier, counter);
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
                Thread.Sleep(0); // Thread.yield(); in java
            }
            else
            {
                --counter;
            }

            return counter;
        }
    }
}