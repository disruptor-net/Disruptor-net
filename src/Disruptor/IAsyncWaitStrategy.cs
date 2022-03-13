using System.Threading;
using System.Threading.Tasks;

namespace Disruptor;

/// <summary>
/// <see cref="IWaitStrategy"/> that supports asynchronous event processors.
/// </summary>
public interface IAsyncWaitStrategy : IWaitStrategy
{
    /// <summary>
    /// Wait for the given sequence to be available.
    /// </summary>
    /// <seealso cref="IWaitStrategy.WaitFor"/>
    /// <param name="sequence">sequence to be waited on</param>
    /// <param name="dependentSequences">sequences on which to wait</param>
    /// <param name="cancellationToken">processing cancellation token</param>
    /// <returns>either the sequence that is available (which may be greater than the requested sequence), or a timeout</returns>
    ValueTask<SequenceWaitResult> WaitForAsync(long sequence, DependentSequenceGroup dependentSequences, CancellationToken cancellationToken);
}
