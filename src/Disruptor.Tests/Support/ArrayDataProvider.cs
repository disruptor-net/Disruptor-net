using System;

namespace Disruptor.Tests.Support
{
    public class ArrayDataProvider<T> : IDataProvider<T>
        where T : class
    {
        public T[] Data { get; }

        public ArrayDataProvider(int capacity) : this(new T[capacity])
        {
        }

        public ArrayDataProvider(T[] data)
        {
            Data = data;
        }

        public T this[long sequence] => Data[sequence % Data.Length];

#if DISRUPTOR_V5
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

        public EventBatch<T> GetBatch(long lo, long hi)
        {
            var index1 = (int)(lo % Data.Length);
            var index2 = (int)(hi % Data.Length);

            if (index1 <= index2)
                return new EventBatch<T>(Data, index1, index2 - index1 + 1);

            return new EventBatch<T>(Data, index1, Data.Length - index1);
        }
#endif
    }
}
