using System;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor.Processing;

public static class EventProcessorExtensions
{
    /// <summary>
    /// Starts the processor on the default task scheduler.
    /// </summary>
    /// <returns>A task that represents the startup of the processor.</returns>
    public static Task Start(this IEventProcessor eventProcessor)
    {
        return eventProcessor.Start(TaskScheduler.Default);
    }

    internal static Task StartLongRunningTask(this TaskScheduler taskScheduler, Action action)
    {
        return Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.LongRunning, taskScheduler);
    }
}
