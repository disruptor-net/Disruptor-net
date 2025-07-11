using System.Threading.Tasks;

namespace Disruptor.Dsl;

internal interface IConsumerInfo
{
    Sequence[] Sequences { get; }

    DependentSequenceGroup? DependentSequences { get; }

    bool IsEndOfChain { get; }

    Task Start(TaskScheduler taskScheduler);

    Task Halt();

    void MarkAsUsedInBarrier();

    bool IsRunning { get; }
}
