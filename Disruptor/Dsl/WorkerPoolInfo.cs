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

        public ISequence[] Sequences => _workerPool.WorkerSequences;

        public ISequenceBarrier Barrier { get; }

        public bool IsEndOfChain { get; private set; }

        public void Start(IExecutor executor)
        {
            _workerPool.Start(executor);
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