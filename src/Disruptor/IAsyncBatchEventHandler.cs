using System.Threading.Tasks;

namespace Disruptor
{
    public interface IAsyncBatchEventHandler<T> where T : class
    {
        ValueTask OnBatch(EventBatch<T> batch, long sequence);
    }
}
