using System.Threading;
using Disruptor.Processing;

namespace Disruptor.Samples.Wiki.WaitStrategies;

public class WaitStrategyV7MigrationSample
{
    public class WaitStrategyV6 : IWaitStrategyV6
    {
        private readonly object _gate = new();

        public bool IsBlockingStrategy => true;

        public SequenceWaitResult WaitFor(long sequence, DependentSequenceGroup dependentSequences, CancellationToken cancellationToken)
        {
            if (dependentSequences.CursorValue < sequence)
            {
                lock (_gate)
                {
                    while (dependentSequences.CursorValue < sequence)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Monitor.Wait(_gate);
                    }
                }
            }

            return dependentSequences.AggressiveSpinWaitFor(sequence, cancellationToken);
        }

        public void SignalAllWhenBlocking()
        {
            lock (_gate)
            {
                Monitor.PulseAll(_gate);
            }
        }
    }

    public class WaitStrategyV7 : IWaitStrategy
    {
        private readonly object _gate = new();

        public bool IsBlockingStrategy => true;

        public ISequenceWaiter NewSequenceWaiter(SequenceWaiterOwner owner, DependentSequenceGroup dependentSequences)
        {
            return new SequenceWaiter(_gate, dependentSequences);
        }

        public void SignalAllWhenBlocking()
        {
            lock (_gate)
            {
                Monitor.PulseAll(_gate);
            }
        }

        private class SequenceWaiter(object gate, DependentSequenceGroup dependentSequences) : ISequenceWaiter
        {
            public SequenceWaitResult WaitFor(long sequence, CancellationToken cancellationToken)
            {
                if (dependentSequences.CursorValue < sequence)
                {
                    lock (gate)
                    {
                        while (dependentSequences.CursorValue < sequence)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            Monitor.Wait(gate);
                        }
                    }
                }

                return dependentSequences.AggressiveSpinWaitFor(sequence, cancellationToken);
            }

            public void Cancel()
            {
                lock (gate)
                {
                    Monitor.PulseAll(gate);
                }
            }

            public void Dispose()
            {
            }
        }
    }

    /// <summary>
    /// Strategy employed for making <see cref="IEventProcessor"/> wait on a cursor <see cref="Sequence"/>.
    /// </summary>
    private interface IWaitStrategyV6
    {
        /// <summary>
        /// Indicates whether this wait strategy is based on blocking synchronization primitives
        /// and if <see cref="SignalAllWhenBlocking"/> should be invoked.
        /// </summary>
        /// <remarks>
        /// Please implement this property as a constant to help the JIT remove unnecessary branches.
        /// The value of this property is not expected to change for a given wait strategy type.
        /// </remarks>
        bool IsBlockingStrategy { get; }

        /// <summary>
        /// Wait for the given sequence to be available. It is possible for this method to return a value
        /// less than the sequence number supplied depending on the implementation of the WaitStrategy. A common
        /// use for this is to signal a timeout. Any event process that is using a wait strategy to get notifications
        /// about message becoming available should remember to handle this case. The <see cref="IEventProcessor"/> explicitly
        /// handles this case and will signal a timeout if required.
        /// </summary>
        /// <param name="sequence">sequence to be waited on</param>
        /// <param name="dependentSequences">sequences on which to wait</param>
        /// <param name="cancellationToken">processing cancellation token</param>
        /// <returns>either the sequence that is available (which may be greater than the requested sequence), or a timeout</returns>
        SequenceWaitResult WaitFor(long sequence, DependentSequenceGroup dependentSequences, CancellationToken cancellationToken);

        /// <summary>
        /// Signal those <see cref="IEventProcessor"/> waiting that the cursor has advanced.
        /// Only invoked when <see cref="IsBlockingStrategy"/> is true.
        /// </summary>
        void SignalAllWhenBlocking();
    }
}
