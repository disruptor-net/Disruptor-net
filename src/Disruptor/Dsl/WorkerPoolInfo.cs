using System.Threading.Tasks;
using Disruptor.Processing;

namespace Disruptor.Dsl;

internal class WorkerPoolInfo<T> : IConsumerInfo where T : class
{
    private readonly WorkerPool<T> _workerPool;

    public WorkerPoolInfo(WorkerPool<T> workerPool, DependentSequenceGroup dependentSequences)
    {
        _workerPool = workerPool;
        DependentSequences = dependentSequences;
        IsEndOfChain = true;
    }

    public ISequence[] Sequences => _workerPool.GetWorkerSequences();

    public DependentSequenceGroup DependentSequences { get; }

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
