using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Disruptor.PerfTests.Support
{
    class LockFreeBoundedQueue<T> : IProducerConsumerCollection<T>
    {
        private readonly T[] _queue;
        private volatile int _tail;
        private volatile int _head;

        public LockFreeBoundedQueue(int capacity)
        {
            _queue = new T[capacity + 1];
            _tail = _head = 0;
        }

        public bool TryAdd(T item)
        {
            var newtail = (_tail + 1) % _queue.Length;
            if (newtail == _head)
                return false;

            _queue[_tail] = item;
            _tail = newtail;
            return true;
        }

        public bool TryTake(out T item)
        {
            item = default(T);
            if (_head == _tail)
                return false;

            item = _queue[_head];
            _queue[_head] = default(T);
            _head = (_head + 1) % _queue.Length;
            return true;
        }

        public void CopyTo(T[] array, int index)
        {
            throw new NotImplementedException();
        }

        public T[] ToArray()
        {
            throw new NotImplementedException();
        }

        public IEnumerator<T> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public int Count => 0;

        public object SyncRoot { get; } = new object();
        public bool IsSynchronized { get; } = false;
    }
}