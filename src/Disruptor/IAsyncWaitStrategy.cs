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
    /// <param name="sequence">sequence to be waited on</param>
    /// <param name="asyncWaitState">TODO</param>
    /// <returns>either the sequence that is available (which may be greater than the requested sequence), or a timeout</returns>
    /// <seealso cref="IWaitStrategy.WaitFor"/>
    ValueTask<SequenceWaitResult> WaitForAsync(long sequence, AsyncWaitState asyncWaitState);
}
