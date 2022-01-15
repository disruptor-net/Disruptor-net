namespace Disruptor.Tests.Support;

public class DummyEventHandler<T> : IEventHandler<T>
{
    public int StartCalls { get; private set; }
    public int ShutdownCalls { get; private set; }
    public T? LastEvent { get; private set; }
    public long LastSequence { get; private set; }

    public void OnStart() => StartCalls++;

    public void OnShutdown() => ShutdownCalls++;

    public void OnEvent(T data, long sequence, bool endOfBatch)
    {
        LastEvent = data;
        LastSequence = sequence;
    }
}