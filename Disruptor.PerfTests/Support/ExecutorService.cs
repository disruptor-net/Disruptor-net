using System.Threading.Tasks;

namespace Disruptor.PerfTests.Support
{
    class ExecutorService<T> where T : class
    {
        public void Submit(BatchEventProcessor<T> eventProcessor)
        {
            Task.Factory.StartNew(eventProcessor.Run);
        }
    }
}