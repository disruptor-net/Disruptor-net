using Disruptor.Processing;

namespace Disruptor;

public interface IValueRingBuffer<T> : ICursored, ISequenced, IValueDataProvider<T>
    where T : struct
{
    void AddGatingSequences(params Sequence[] gatingSequences);
    bool RemoveGatingSequence(Sequence sequence);
    long GetMinimumGatingSequence();

    SequenceBarrier NewBarrier(SequenceWaiterOwner owner, params Sequence[] sequencesToTrack);
    IValueEventProcessor<T> CreateEventProcessor(SequenceBarrier barrier, IValueEventHandler<T> eventHandler);
}
