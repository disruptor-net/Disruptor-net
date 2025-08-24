using System;
using System.Threading;
using Disruptor.Processing;

namespace Disruptor;

/// <summary>
/// Strategy used by the IPC event processors to wait until sequence values are available for processing.
/// </summary>
/// <remarks>
/// The wait strategy is used to wait until events are published (publisher / processor synchronization)
/// but also until events are processed by the previous processors (processor / processor synchronization).
/// </remarks>
public interface IIpcWaitStrategy
{
    /// <summary>
    /// Creates the <see cref="ISequenceWaiter"/> that will be used by an event processor to wait for available sequences.
    /// </summary>
    /// <param name="owner">The owner of the sequence waiter.</param>
    /// <param name="dependentSequences">The dependent sequences of the target event processor.</param>
    /// <returns>The sequence waiter.</returns>
    IIpcSequenceWaiter NewSequenceWaiter(SequenceWaiterOwner owner, IpcDependentSequenceGroup dependentSequences);
}
