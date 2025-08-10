using System;
using System.Threading;
using Disruptor.Processing;

namespace Disruptor;

/// <summary>
/// Represents a sequence waiter created by a <see cref="IIpcWaitStrategy"/>.
/// It is used by event processor to wait for available sequences.
/// </summary>
public interface IIpcSequenceWaiter
{
    /// <summary>
    /// Waits for the given sequence to be available. It is possible for this method to return a value
    /// less than the sequence number supplied depending on the implementation of the wait strategy. A common
    /// use for this is to signal a timeout. Any event process that is using a wait strategy to get notifications
    /// about message becoming available should remember to handle this case. The <see cref="IEventProcessor"/> explicitly
    /// handles this case and will signal a timeout if required.
    /// </summary>
    /// <param name="sequence">sequence to be waited on</param>
    /// <param name="cancellationToken">processing cancellation token</param>
    /// <returns>either the sequence that is available (which may be greater than the requested sequence), or a timeout</returns>
    SequenceWaitResult WaitFor(long sequence, CancellationToken cancellationToken);
}
