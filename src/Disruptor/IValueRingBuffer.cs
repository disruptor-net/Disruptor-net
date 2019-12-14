namespace Disruptor
{
    public interface IValueRingBuffer<T> : ICursored, ISequenced, IValueDataProvider<T>
        where T : struct
    {
        void AddGatingSequences(params ISequence[] gatingSequences);
        bool RemoveGatingSequence(ISequence sequence);
        long GetMinimumGatingSequence();

        ISequenceBarrier NewBarrier(params ISequence[] sequencesToTrack);

        void ResetTo(long sequence);
    }
}
