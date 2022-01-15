namespace Disruptor.Tests.Support;

public class ArrayValueDataProvider<T> : IValueDataProvider<T>
{
    public T[] Data { get; }

    public ArrayValueDataProvider(int capacity) : this(new T[capacity])
    {
    }

    public ArrayValueDataProvider(T[] data)
    {
        Data = data;
    }


    public ref T this[long sequence] => ref Data[sequence % Data.Length];
}