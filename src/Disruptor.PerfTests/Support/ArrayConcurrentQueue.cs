using System.Threading;

namespace Disruptor.PerfTests.Support;

/// <summary>
/// Single producer / single consumer bounded queue.
/// </summary>
/// <typeparam name="T"></typeparam>
public class ArrayConcurrentQueue<T>
{
    private readonly T[] _queue;
    private volatile int _tail;
    private volatile int _head;

    public ArrayConcurrentQueue(int capacity)
    {
        _queue = new T[capacity + 1];
        _tail = _head = 0;
    }

    public bool TryEnqueue(T item)
    {
        var newtail = (_tail + 1) % _queue.Length;
        if (newtail == _head)
            return false;

        _queue[_tail] = item;
        _tail = newtail;
        return true;
    }

    public void Enqueue(T item)
    {
        while (!TryEnqueue(item))
            Thread.Yield();
    }

    public bool TryDequeue(out T item)
    {
        item = default(T);
        if (_head == _tail)
            return false;

        item = _queue[_head];
        _queue[_head] = default(T);
        _head = (_head + 1) % _queue.Length;
        return true;
    }

    public T Dequeue()
    {
        T item;
        while (!TryDequeue(out item))
            Thread.Yield();
        return item;
    }
}