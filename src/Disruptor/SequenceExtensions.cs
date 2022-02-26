using System.Runtime.CompilerServices;
using System.Threading;

namespace Disruptor;

public static class SequenceExtensions
{
    /// <summary>
    /// Waits until the sequence value is greater than or equal to the expected value using <see cref="AggressiveSpinWait"/>.
    /// </summary>
    /// <returns>the sequence value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long AggressiveSpinWaitFor(this ISequence sequence, long expectedValue, CancellationToken cancellationToken)
    {
        var availableSequence = sequence.Value;
        if (availableSequence >= expectedValue)
            return availableSequence;

        return AggressiveSpinWaitForImpl(sequence, expectedValue, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long AggressiveSpinWaitForImpl(ISequence sequence, long expectedValue, CancellationToken cancellationToken)
    {
        var aggressiveSpinWait = new AggressiveSpinWait();
        long availableSequence;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            aggressiveSpinWait.SpinOnce();
            availableSequence = sequence.Value;
        }
        while (availableSequence < expectedValue);

        return availableSequence;
    }

    /// <summary>
    /// Waits until the sequence value is greater than or equal to the expected value using <see cref="SpinWait"/>.
    /// </summary>
    /// <returns>the sequence value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long SpinWaitFor(this ISequence sequence, long expectedValue, CancellationToken cancellationToken)
    {
        var availableSequence = sequence.Value;
        if (availableSequence >= expectedValue)
            return availableSequence;

        return SpinWaitForImpl(sequence, expectedValue, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long SpinWaitForImpl(ISequence sequence, long expectedValue, CancellationToken cancellationToken)
    {
        var spinWait = new SpinWait();
        long availableSequence;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            spinWait.SpinOnce();
            availableSequence = sequence.Value;
        }
        while (availableSequence < expectedValue);

        return availableSequence;
    }
}
