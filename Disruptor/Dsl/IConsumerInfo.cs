namespace Disruptor.Dsl
{
    public interface IConsumerInfo
    {
        Sequence[] Sequences { get; }

        ISequenceBarrier Barrier { get; }

        bool IsEndOfChain { get; }

        void Start(IExecutor executor);

        void Halt();

        void MarkAsUsedInBarrier();

        bool IsRunning { get; }
    }
}