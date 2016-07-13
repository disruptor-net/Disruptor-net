using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Disruptor.PerfTests.WorkHandler
{
    class LockFreeBoundedQueue<T> : IProducerConsumerCollection<T>
    {
        private readonly T[] _values;
        private long _tailPointer;
        private long _headPointer;

        public LockFreeBoundedQueue(int size)
        {
            _values = new T[size];
        }

        public bool TryAdd(T e)
        {
            var curTail = _tailPointer;
            var diff = curTail - _values.Length;
            if (_headPointer <= diff)
                return false;

            _values[(int)(curTail % _values.Length)] = e;
            Interlocked.Increment(ref _tailPointer);
            return true;
        }

        public bool TryTake(out T value)
        {
            var curHead = _headPointer;
            if (curHead >= _tailPointer)
            {
                value = default(T);
                return false;
            }

            if (Interlocked.CompareExchange(ref _headPointer, curHead + 1, curHead) != curHead)
            {
                value = default(T);
                return false;
            }

            var index = (int)curHead % _values.Length;

            value = _values[index];
            _values[index] = default(T);
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

        public int Count => (int)(_headPointer - _tailPointer);
        public object SyncRoot { get; } = new object();
        public bool IsSynchronized { get; } = false;
    }
}