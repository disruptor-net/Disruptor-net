using System;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor;

/// <summary>
/// Coordination barrier used by asynchronous processors to wait before processing events.
/// </summary>
/// <remarks>
/// The barrier may be shared by multiple handlers.
/// </remarks>
public interface IAsyncSequenceBarrier
{
    /// <summary>
    /// Wait for the given sequence to be available for consumption.
    /// </summary>
    /// <param name="sequence">sequence to wait for</param>
    /// <returns>the sequence up to which is available</returns>
    /// <exception cref="OperationCanceledException">if a status change has occurred for the Disruptor</exception>
    ValueTask<SequenceWaitResult> WaitForAsync(long sequence);

    /// <summary>
    /// The <see cref="DependentSequenceGroup"/> that contains the sequences of the processors
    /// that must run before the barrier processors.
    /// </summary>
    DependentSequenceGroup DependentSequences { get; }

    /// <summary>
    /// The cancellation token used to stop the processing.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Resets the <see cref="CancellationToken"/>.
    /// </summary>
    void ResetProcessing();

    /// <summary>
    /// Cancels the <see cref="CancellationToken"/>.
    /// </summary>
    void CancelProcessing();
}
