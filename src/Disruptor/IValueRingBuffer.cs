namespace Disruptor;

public interface IValueRingBuffer<T> : ICursored, ISequenced, IValueDataProvider<T>
    where T : struct
{
    void AddGatingSequences(params Sequence[] gatingSequences);
    bool RemoveGatingSequence(Sequence sequence);
    long GetMinimumGatingSequence();

    SequenceBarrier NewBarrier(params Sequence[] sequencesToTrack);
}
