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
internal interface IIpcEventProcessor<T> : IDisposable, IAsyncDisposable
    where T : unmanaged
{
    /// <summary>
    /// Return a reference to the <see cref="SequencePointer"/> being used by this <see cref="IEventProcessor"/>
    /// </summary>
    SequencePointer SequencePointer { get; }

    /// <summary>
    /// Halts the processor.
    /// <remarks>
    /// If the processor is processing an event batch when <see cref="Halt"/> is invoked, it will complete processing the current batch before halting.
    /// </remarks>
    /// </summary>
    /// <returns>
    /// A task that represents the shutdown of the processor.
    /// The task completes after <see cref="IEventHandler.OnShutdown"/> is invoked.
    /// </returns>
    Task Halt();

    /// <summary>
    /// Starts the processor.
    /// </summary>
    /// <returns>
    /// A task that represents the startup of the processor.
    /// The task completes after <see cref="IEventHandler.OnStart"/> is invoked.
    /// </returns>
    Task Start(TaskScheduler taskScheduler);

    /// <summary>
    /// Indicates whether the processor is running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Set a new <see cref="IValueExceptionHandler{T}"/> for handling exceptions propagated out of the <see cref="IValueEventHandler{T}"/>
    /// </summary>
    /// <param name="exceptionHandler">exceptionHandler to replace the existing exceptionHandler.</param>
    void SetExceptionHandler(IValueExceptionHandler<T> exceptionHandler);
}
