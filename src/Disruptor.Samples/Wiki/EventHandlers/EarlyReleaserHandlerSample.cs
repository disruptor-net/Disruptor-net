namespace Disruptor.Samples.Wiki.EventHandlers;

public class EarlyReleaserHandlerSample
{
    public class Event
    {
    }

public class Handler : IEventHandler<Event>, IEventProcessorSequenceAware
{
    private Sequence _sequenceCallback;

    public void SetSequenceCallback(Sequence sequenceCallback)
    {
        _sequenceCallback = sequenceCallback;
    }

    public void OnEvent(Event data, long sequence, bool endOfBatch)
    {
        ProcessEvent(data);

        // Can be invoked for each event or using a custom logic
        _sequenceCallback.SetValue(sequence);
    }

    private void ProcessEvent(Event data)
    {
        //
    }
}
}
