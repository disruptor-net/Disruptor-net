namespace Disruptor;

/// <summary>
/// <see cref="IWaitStrategy"/> that supports asynchronous event processors.
/// </summary>
public interface IAsyncWaitStrategy : IWaitStrategy
{
    /// <summary>
    /// Creates the <see cref="IAsyncSequenceWaiter"/> that will be used by an asynchronous event processor to wait for available sequences.
    /// </summary>
    /// <param name="eventHandler">The event handler of the target event processor. Can be null for custom event processors or if the event processor is a <see cref="IWorkHandler{T}"/> processor.</param>
    /// <param name="dependentSequences">The dependent sequences of the target event processor.</param>
    /// <returns>The sequence waiter.</returns>
    IAsyncSequenceWaiter NewAsyncSequenceWaiter(IEventHandler? eventHandler, DependentSequenceGroup dependentSequences);
}
