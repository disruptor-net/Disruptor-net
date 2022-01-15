using System;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor.Processing;

public static class EventProcessorExtensions
{
    public static Task Start(this IEventProcessor eventProcessor)
    {
        return eventProcessor.Start(TaskScheduler.Default, TaskCreationOptions.None);
    }

    public static Task StartLongRunning(this IEventProcessor eventProcessor)
    {
        return eventProcessor.StartLongRunning(TaskScheduler.Default);
    }

    public static Task StartLongRunning(this IEventProcessor eventProcessor, TaskScheduler taskScheduler)
    {
        return eventProcessor.Start(taskScheduler, TaskCreationOptions.LongRunning);
    }

    internal static Task ScheduleAndStart(this TaskScheduler taskScheduler, Action action, TaskCreationOptions taskCreationOptions)
    {
        return Task.Factory.StartNew(action, CancellationToken.None, taskCreationOptions, taskScheduler);
    }
}