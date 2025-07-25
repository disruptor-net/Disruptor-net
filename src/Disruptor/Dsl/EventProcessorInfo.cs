﻿using System.Threading.Tasks;
using Disruptor.Processing;

namespace Disruptor.Dsl;

/// <summary>
/// Wrapper class to tie together a particular event processing stage
///
/// Tracks the event processor instance, the event handler instance, and sequence barrier which the stage is attached to.
/// </summary>
internal class EventProcessorInfo : IConsumerInfo
{
    public EventProcessorInfo(IEventProcessor eventProcessor, IEventHandler? eventHandler, DependentSequenceGroup? dependentSequences, bool owned)
    {
        EventProcessor = eventProcessor;
        Handler = eventHandler;
        DependentSequences = dependentSequences;
        Owned = owned;
        IsEndOfChain = true;
    }

    public IEventProcessor EventProcessor { get; }

    public Sequence[] Sequences => new[] { EventProcessor.Sequence };

    public IEventHandler? Handler { get; }

    public DependentSequenceGroup? DependentSequences { get; }

    /// <summary>
    /// Indicates that the event processor was created by the disruptor and thus can be disposed when
    /// the disruptor is disposed.
    /// </summary>
    public bool Owned { get; }

    public bool IsEndOfChain { get; private set; }

    public Task Start(TaskScheduler taskScheduler)
    {
        return EventProcessor.Start(taskScheduler);
    }

    public Task Halt()
    {
        return EventProcessor.Halt();
    }

    public void MarkAsUsedInBarrier()
    {
        IsEndOfChain = false;
    }

    public bool IsRunning => EventProcessor.IsRunning;

    public void Dispose()
    {
        if (Owned)
            EventProcessor.Dispose();
        else
            EventProcessor.Halt();
    }
}
