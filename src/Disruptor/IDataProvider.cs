using System;

namespace Disruptor
{
    public interface IDataProvider<T>
    {
        T this[long sequence] { get; }

#if NETCOREAPP
        ReadOnlySpan<T> this[long lo, long hi] { get; }
#endif
    }
}
