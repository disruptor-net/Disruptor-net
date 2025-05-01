namespace Disruptor;

/// <summary>
/// <see cref="IWaitStrategy"/> that supports asynchronous event processors.
/// </summary>
public interface IAsyncWaitStrategy : IWaitStrategy
{
    /// <summary>
    /// Creates the <see cref="IAsyncSequenceWaiter"/> that will be used by an asynchronous event processor to wait for available sequences.
    /// </summary>
    /// <param name="owner">The owner of the sequence waiter.</param>
    /// <param name="dependentSequences">The dependent sequences of the target event processor.</param>
    /// <returns>The sequence waiter.</returns>
    IAsyncSequenceWaiter NewAsyncSequenceWaiter(SequenceWaiterOwner owner, DependentSequenceGroup dependentSequences);
}
