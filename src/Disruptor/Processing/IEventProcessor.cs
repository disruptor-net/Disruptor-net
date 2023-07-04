using System;
using System.Threading.Tasks;

namespace Disruptor.Processing;

/// <summary>
/// An event processor needs to be an implementation of a runnable that will poll for events from the ring buffer
/// using the appropriate wait strategy.
///
/// It is unlikely that you will need to implement this interface yourself.
/// Event processors are automatically created by the disruptor for your event handlers.
///
/// An event process will generally be associated with a thread (long running task) for execution.
/// </summary>
public interface IEventProcessor
{
    /// <summary>
    /// Return a reference to the <see cref="Sequence"/> being used by this <see cref="IEventProcessor"/>
    /// </summary>
    Sequence Sequence { get; }

    /// <summary>
    /// Signal that this <see cref="IEventProcessor"/> should stop when it has finished consuming at the next clean break.
    /// It will call <see cref="SequenceBarrier.CancelProcessing"/> to notify the thread to check status.
    /// </summary>
    void Halt();

    /// <summary>
    /// Starts this processor.
    /// </summary>
    Task Start(TaskScheduler taskScheduler, TaskCreationOptions taskCreationOptions);

    /// <summary>
    /// Waits before the event processor enters the <see cref="IEventProcessor.IsRunning"/> state.
    /// </summary>
    /// <param name="timeout">Maximum wait duration</param>
    void WaitUntilStarted(TimeSpan timeout);

    /// <summary>
    /// Gets if the processor is running
    /// </summary>
    bool IsRunning { get; }
}
