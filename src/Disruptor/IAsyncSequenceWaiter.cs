using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;

namespace Disruptor;

/// <summary>
/// Strategy used by the event processors to wait until sequence values are available for processing.
/// </summary>
/// <remarks>
/// The wait strategy is used to wait until events are published (publisher / processor synchronization)
/// but also until events are processed by the previous processors (processor / processor synchronization).
/// </remarks>
public interface IAsyncSequenceWaiter
{
    DependentSequenceGroup DependentSequences { get; }

    /// <summary>
    /// Wait for the given sequence to be available. It is possible for this method to return a value
    /// less than the sequence number supplied depending on the implementation of the wait strategy. A common
    /// use for this is to signal a timeout. Any event process that is using a wait strategy to get notifications
    /// about message becoming available should remember to handle this case. The <see cref="IEventProcessor"/> explicitly
    /// handles this case and will signal a timeout if required.
    /// </summary>
    /// <param name="sequence">sequence to be waited on</param>
    /// <param name="cancellationToken">processing cancellation token</param>
    /// <returns>either the sequence that is available (which may be greater than the requested sequence), or a timeout</returns>
    ValueTask<SequenceWaitResult> WaitForAsync(long sequence, CancellationToken cancellationToken);

    /// <summary>
    /// Requests cancellation for all active waits.
    /// </summary>
    /// <remarks>
    /// Useful for sequence wait strategies that do not rely on the <see cref="CancellationToken"/>.
    /// </remarks>
    void Cancel();
}
