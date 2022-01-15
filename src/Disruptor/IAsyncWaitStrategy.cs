using System.Threading;
using System.Threading.Tasks;

namespace Disruptor;

/// <summary>
/// <see cref="IWaitStrategy"/> that supports asynchronous event processors.
/// </summary>
public interface IAsyncWaitStrategy : IWaitStrategy
{
    /// <summary>
    /// Wait for the given sequence to be available. <see cref="IWaitStrategy.WaitFor"/>
    /// </summary>
    /// <param name="sequence">sequence to be waited on</param>
    /// <param name="cursor">main sequence from the ring buffer</param>
    /// <param name="dependentSequence">sequence on which to wait</param>
    /// <param name="cancellationToken">processing cancellation token</param>
    /// <returns>either the sequence that is available (which may be greater than the requested sequence), or a timeout</returns>
    ValueTask<SequenceWaitResult> WaitForAsync(long sequence, Sequence cursor, ISequence dependentSequence, CancellationToken cancellationToken);
}