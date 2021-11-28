using System.Threading;
using Disruptor.Processing;

namespace Disruptor
{
    /// <summary>
    /// Yielding strategy that uses a Thread.Yield() for <see cref="IEventProcessor"/>s waiting on a barrier
    /// after an initially spinning.
    ///
    /// This strategy is a good compromise between performance and CPU resource without incurring significant latency spikes.
    /// </summary>
    public sealed class YieldingWaitStrategy : INonBlockingWaitStrategy
    {
        private const int _spinTries = 100;

        public long WaitFor(long sequence, Sequence cursor, ISequence dependentSequence, CancellationToken cancellationToken)
        {
            long availableSequence;
            var counter = _spinTries;

            while ((availableSequence = dependentSequence.Value) < sequence)
            {
                counter = ApplyWaitMethod(cancellationToken, counter);
            }

            return availableSequence;
        }

        /// <summary>
        /// Signal those <see cref="IEventProcessor"/> waiting that the cursor has advanced.
        /// </summary>
        public void SignalAllWhenBlocking()
        {
        }

        private static int ApplyWaitMethod(CancellationToken cancellationToken, int counter)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if(counter == 0)
            {
                Thread.Yield();
            }
            else
            {
                --counter;
            }

            return counter;
        }
    }
}
