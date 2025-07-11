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

    public Sequence[] Sequences => _workerPool.GetWorkerSequences();

    public DependentSequenceGroup DependentSequences { get; }

    public bool IsEndOfChain { get; private set; }

    public Task Start(TaskScheduler taskScheduler)
    {
        return _workerPool.Start(taskScheduler);
    }

    public Task Halt()
    {
        return _workerPool.Halt();
    }

    public void MarkAsUsedInBarrier()
    {
        IsEndOfChain = false;
    }

    public bool IsRunning => _workerPool.IsRunning;

    public void Dispose()
    {
        _workerPool.Dispose();
    }
}
