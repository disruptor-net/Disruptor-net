using System;
using System.Runtime.CompilerServices;

namespace Disruptor.Processing;

public static class EventProcessorHelpers
{
    public readonly struct NoopOnBatchStartEvaluator : IOnBatchStartEvaluator
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldInvokeOnBatchStart(long availableSequence, long nextSequence)
        {
            return false;
        }
    }

    public readonly struct DefaultOnBatchStartEvaluator : IOnBatchStartEvaluator
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldInvokeOnBatchStart(long availableSequence, long nextSequence)
        {
            return availableSequence >= nextSequence;
        }
    }

    public readonly struct DefaultBatchSizeLimiter(int maxBatchOffset) : IBatchSizeLimiter
    {
        private readonly int _maxBatchOffset = maxBatchOffset - 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ApplyMaxBatchSize(long availableSequence, long nextSequence)
        {
            return Math.Min(availableSequence, nextSequence + _maxBatchOffset);
        }
    }

    public readonly struct NoopBatchSizeLimiter : IBatchSizeLimiter
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ApplyMaxBatchSize(long availableSequence, long nextSequence)
        {
            return availableSequence;
        }
    }

    public readonly struct NoopPublishedSequenceReader : IPublishedSequenceReader
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetHighestPublishedSequence(long nextSequence, long availableSequence)
        {
            return availableSequence;
        }
    }

    public readonly struct MultiProducerSequencerPublishedSequenceReader(MultiProducerSequencer sequencer) : IPublishedSequenceReader
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetHighestPublishedSequence(long nextSequence, long availableSequence)
        {
            return sequencer.GetHighestPublishedSequence(nextSequence, availableSequence);
        }
    }

    internal readonly struct IpcSequencerPublishedSequenceReader(IpcSequencer sequencer) : IPublishedSequenceReader
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetHighestPublishedSequence(long nextSequence, long availableSequence)
        {
            return sequencer.GetHighestPublishedSequence(nextSequence, availableSequence);
        }
    }

    public readonly struct UnknownSequencerPublishedSequenceReader(ISequencer sequencer) : IPublishedSequenceReader
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetHighestPublishedSequence(long nextSequence, long availableSequence)
        {
            return sequencer.GetHighestPublishedSequence(nextSequence, availableSequence);
        }
    }

    public readonly struct ValueRingBufferDataProvider<T>(ValueRingBuffer<T> ringBuffer) : IValueDataProvider<T>
        where T : struct
    {
        public ref T this[long sequence]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref ringBuffer[sequence];
        }
    }

    public readonly struct UnmanagedRingBufferDataProvider<T>(UnmanagedRingBuffer<T> ringBuffer) : IValueDataProvider<T>
        where T : unmanaged
    {
        public ref T this[long sequence]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref ringBuffer[sequence];
        }
    }
}
