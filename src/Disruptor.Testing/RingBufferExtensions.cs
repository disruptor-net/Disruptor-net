using Disruptor.Processing;

namespace Disruptor;

public static class RingBufferExtensions
{
    public static SequenceBarrier NewBarrier(this RingBuffer ringBuffer, params Sequence[] sequencesToTrack)
    {
        return ringBuffer.NewBarrier(SequenceWaiterOwner.Unknown, sequencesToTrack);
    }

    internal static IpcSequenceBarrier NewBarrier<T>(this IpcRingBuffer<T> ringBuffer, params SequencePointer[] sequencesToTrack)
        where T : unmanaged
    {
        return ringBuffer.NewBarrier(SequenceWaiterOwner.Unknown, sequencesToTrack);
    }

    public static AsyncSequenceBarrier NewAsyncBarrier<T>(this RingBuffer<T> ringBuffer, params Sequence[] sequencesToTrack)
        where T : class
    {
        return ringBuffer.NewAsyncBarrier(SequenceWaiterOwner.Unknown, sequencesToTrack);
    }

    public static SequenceBarrier NewBarrier<T>(this UnmanagedRingBuffer<T> ringBuffer, params Sequence[] sequencesToTrack)
        where T : unmanaged
    {
        return ringBuffer.NewBarrier(SequenceWaiterOwner.Unknown, sequencesToTrack);
    }

    public static SequenceBarrier NewBarrier<T>(this IValueRingBuffer<T> ringBuffer, params Sequence[] sequencesToTrack)
        where T : struct
    {
        return ringBuffer.NewBarrier(SequenceWaiterOwner.Unknown, sequencesToTrack);
    }

    public static SequenceBarrier NewBarrier<T>(this ValueRingBuffer<T> ringBuffer, params Sequence[] sequencesToTrack)
        where T : struct
    {
        return ringBuffer.NewBarrier(SequenceWaiterOwner.Unknown, sequencesToTrack);
    }

    public static PerfTestIpcEventProcessor<T> CreatePerfTestEventProcessor<T>(this IpcRingBuffer<T> ringBuffer, IValueEventHandler<T> handler)
        where T : unmanaged
    {
        var sequence = ringBuffer.NewSequence();
        var sequenceBarrier = ringBuffer.NewBarrier();

        var eventProcessor = EventProcessorFactory.Create(ringBuffer, sequence, sequenceBarrier, handler);

        ringBuffer.SetGatingSequences(eventProcessor.SequencePointer);

        return new PerfTestIpcEventProcessor<T>(eventProcessor);
    }
}
