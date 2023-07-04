using System.Threading.Tasks;
using Disruptor.Processing;

namespace Disruptor.Dsl;

/// <summary>
/// Wrapper class to tie together a particular event processing stage
///
/// Tracks the event processor instance, the event handler instance, and sequence barrier which the stage is attached to.
/// </summary>
internal class EventProcessorInfo : IConsumerInfo
{
    public EventProcessorInfo(IEventProcessor eventProcessor, object? eventHandler, DependentSequenceGroup? dependentSequences)
    {
        EventProcessor = eventProcessor;
        Handler = eventHandler;
        DependentSequences = dependentSequences;
        IsEndOfChain = true;
    }

    public IEventProcessor EventProcessor { get; }

    public Sequence[] Sequences => new[] { EventProcessor.Sequence };

    public object? Handler { get; }

    public DependentSequenceGroup? DependentSequences { get; }

    public bool IsEndOfChain { get; private set; }

    public void Start(TaskScheduler taskScheduler)
    {
        EventProcessor.StartLongRunning(taskScheduler);
    }

    public void Halt()
    {
        EventProcessor.Halt();
    }

    public void MarkAsUsedInBarrier()
    {
        IsEndOfChain = false;
    }

    public bool IsRunning => EventProcessor.IsRunning;
}
