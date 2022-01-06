using System;
using System.Threading.Tasks;

#if DISRUPTOR_V5

namespace Disruptor
{
    public interface IAsyncBatchEventHandler<T> where T : class
    {
        ValueTask OnBatch(EventBatch<T> batch, long sequence);
    }
}

#endif
