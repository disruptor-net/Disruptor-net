namespace Disruptor;

/// <summary>
/// An aggregate collection of <see cref="IEventHandler{T}"/> that get called in sequence for each event.
/// </summary>
/// <typeparam name="T">event implementation storing the data for sharing during exchange or parallel coordination of an event</typeparam>
public class AggregateEventHandler<T> : IEventHandler<T>
{
    private readonly IEventHandler<T>[] _eventHandlers;

    public AggregateEventHandler(params IEventHandler<T>[] eventHandlers)
    {
        _eventHandlers = eventHandlers;
    }

    public void OnEvent(T data, long sequence, bool endOfBatch)
    {
        foreach (var eventHandler in _eventHandlers)
        {
            eventHandler.OnEvent(data, sequence, endOfBatch);
        }
    }

    public void OnStart()
    {
        foreach (var eventHandler in _eventHandlers)
        {
            eventHandler.OnStart();
        }
    }

    public void OnShutdown()
    {
        foreach (var eventHandler in _eventHandlers)
        {
            eventHandler.OnShutdown();
        }
    }
}
