using System;
using System.Threading.Tasks;

namespace Disruptor;

/// <summary>
/// Coordination barrier for asynchronous event processors.
/// </summary>
/// <seealso cref="ISequenceBarrier"/>
public interface IAsyncSequenceBarrier : ISequenceBarrier
{
    /// <summary>
    /// Wait for the given sequence to be available for consumption.
    /// </summary>
    /// <param name="sequence">sequence to wait for</param>
    /// <returns>the sequence up to which is available</returns>
    /// <exception cref="OperationCanceledException">if a status change has occurred for the Disruptor</exception>
    ValueTask<SequenceWaitResult> WaitForAsync(long sequence);
}
