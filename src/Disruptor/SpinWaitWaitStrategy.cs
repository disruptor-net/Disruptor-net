using System.Threading;
using Disruptor.Processing;

namespace Disruptor
{
    /// <summary>
    /// Spin strategy that uses a <see cref="SpinWait"/> for <see cref="IEventProcessor"/>s waiting on a barrier.
    /// <p>
    /// This strategy is a good compromise between performance and CPU resource.
    /// Latency spikes can occur after quiet periods.
    /// </p>
    /// </summary>
    public sealed class SpinWaitWaitStrategy : INonBlockingWaitStrategy
    {
        /// <summary>
        /// <see cref="IWaitStrategy.WaitFor"/>
        /// </summary>
        public long WaitFor(long sequence, Sequence cursor, ISequence dependentSequence, CancellationToken cancellationToken)
        {
            long availableSequence;

            var spinWait = new SpinWait();
            while ((availableSequence = dependentSequence.Value) < sequence)
            {
                cancellationToken.ThrowIfCancellationRequested();
                spinWait.SpinOnce();
            }

            return availableSequence;
        }

        /// <summary>
        /// <see cref="IWaitStrategy.SignalAllWhenBlocking"/>
        /// </summary>
        public void SignalAllWhenBlocking()
        {
        }
    }
}
