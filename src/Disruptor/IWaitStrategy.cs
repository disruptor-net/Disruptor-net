using System.Threading;
using Disruptor.Processing;

namespace Disruptor
{
    /// <summary>
    /// Strategy employed for making <see cref="IEventProcessor"/> wait on a cursor <see cref="Sequence"/>.
    /// </summary>
    public interface IWaitStrategy
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
        /// <param name="sequence">sequence to be waited on.</param>
        /// <param name="cursor">Ring buffer cursor on which to wait.</param>
        /// <param name="dependentSequence">on which to wait.</param>
        /// <param name="cancellationToken">processing cancellation token</param>
        /// <returns>the wait result</returns>
        SequenceWaitResult WaitFor(long sequence, Sequence cursor, ISequence dependentSequence, CancellationToken cancellationToken);

        /// <summary>
        /// Signal those <see cref="IEventProcessor"/> waiting that the cursor has advanced.
        /// Only invoked when <see cref="IsBlockingStrategy"/> is false.
        /// </summary>
        void SignalAllWhenBlocking();
    }
}
