using System;
using System.Threading;
using Disruptor.Processing;

namespace Disruptor;

/// <summary>
/// Strategy used by the event processors to wait until sequence values are available for processing.
/// </summary>
/// <remarks>
/// The wait strategy is used to wait until events are published (publisher / processor synchronization)
/// but also until events are processed by the previous processors (processor / processor synchronization).
/// </remarks>
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
    /// Creates the <see cref="ISequenceWaiter"/> that will be used by an event processor to wait for available sequences.
    /// </summary>
    /// <param name="eventHandler">The event handler of the target event processor. Can be null for custom event processors or if the event processor is a <see cref="IWorkHandler{T}"/> processor.</param>
    /// <param name="dependentSequences">The dependent sequences of the target event processor.</param>
    /// <returns>The sequence waiter.</returns>
    ISequenceWaiter NewSequenceWaiter(IEventHandler? eventHandler, DependentSequenceGroup dependentSequences);

    /// <summary>
    /// Signals to the waiting event processors that the cursor has advanced.
    /// Only invoked when <see cref="IsBlockingStrategy"/> is true.
    /// </summary>
    void SignalAllWhenBlocking();
}
