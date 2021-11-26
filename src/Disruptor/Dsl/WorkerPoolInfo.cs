using System.Threading.Tasks;
using Disruptor.Processing;

namespace Disruptor.Dsl
{
    public class WorkerPoolInfo<T> : IConsumerInfo where T : class
    {
        private readonly WorkerPool<T> _workerPool;

        public WorkerPoolInfo(WorkerPool<T> workerPool, ISequenceBarrier barrier)
        {
            _workerPool = workerPool;
            Barrier = barrier;
            IsEndOfChain = true;
        }

        public ISequence[] Sequences => _workerPool.GetWorkerSequences();

        public ISequenceBarrier Barrier { get; }

        public bool IsEndOfChain { get; private set; }

        public void Start(TaskScheduler taskScheduler)
        {
            _workerPool.Start(taskScheduler);
        }

        public void Halt()
        {
            _workerPool.Halt();
        }

        public void MarkAsUsedInBarrier()
        {
            IsEndOfChain = false;
        }

        public bool IsRunning => _workerPool.IsRunning;
    }
}
