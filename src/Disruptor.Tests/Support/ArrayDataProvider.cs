using System;

namespace Disruptor.Tests.Support
{
    public class ArrayDataProvider<T> : IDataProvider<T>, IValueDataProvider<T>
    {
        public T[] Data { get; }

        public ArrayDataProvider(int capacity) : this(new T[capacity])
        {
        }

        public ArrayDataProvider(T[] data)
        {
            Data = data;
        }

        T IDataProvider<T>.this[long sequence] => Data[sequence % Data.Length];

#if BATCH_HANDLER
        public ReadOnlySpan<T> this[long lo, long hi]
        {
            get
            {
                var index1 = (int)(lo % Data.Length);
                var index2 = (int)(hi % Data.Length);

                if (index1 <= index2)
                    return new ReadOnlySpan<T>(Data, index1, index2 - index1 + 1);

                return new ReadOnlySpan<T>(Data, index1, Data.Length - index1);
            }
        }
#endif

        ref T IValueDataProvider<T>.this[long sequence] => ref Data[sequence % Data.Length];

        public IDataProvider<T> AsDataProvider() => this;
        public IValueDataProvider<T> AsValueDataProvider() => this;
    }
}
