using System.Threading.Tasks;

namespace Disruptor.PerfTests.Support
{
    class ExecutorService<T> where T : class
    {
        public Task Submit(BatchEventProcessor<T> eventProcessor)
        {
            return Task.Factory.StartNew(eventProcessor.Run);
        }
    }
}