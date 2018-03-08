using System.Threading.Tasks;

namespace Disruptor.PerfTests.Support
{
    public class ExecutorService<T> where T : class
    {
        public Task Submit(IBatchEventProcessor<T> eventProcessor)
        {
            return Task.Factory.StartNew(eventProcessor.Run);
        }
    }
}
