using System.Runtime.CompilerServices;

namespace Disruptor.Processing;

public static class SequenceBarrierExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCancellationRequested<T>(this T sequenceBarrier) where T : ISequenceBarrier
    {
        return sequenceBarrier.CancellationToken.IsCancellationRequested;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfCancellationRequested<T>(this T sequenceBarrier) where T : ISequenceBarrier
    {
        sequenceBarrier.CancellationToken.ThrowIfCancellationRequested();
    }
}