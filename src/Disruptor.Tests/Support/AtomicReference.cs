namespace Disruptor.Tests.Support;

public class AtomicReference<T> where T : class
{
    private volatile T? _reference;

    public AtomicReference(T? reference = null)
    {
        _reference = reference;
    }

    public T? Read()
    {
        return _reference;
    }

    public void Write(T? value)
    {
        _reference = value;
    }
}