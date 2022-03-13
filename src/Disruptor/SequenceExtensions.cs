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
    public static long AggressiveSpinWaitFor(this DependentSequenceGroup sequenceGroup, long expectedValue, CancellationToken cancellationToken)
    {
        var availableSequence = sequenceGroup.Value;
        if (availableSequence >= expectedValue)
            return availableSequence;

        return AggressiveSpinWaitForImpl(sequenceGroup, expectedValue, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long AggressiveSpinWaitForImpl(DependentSequenceGroup sequenceGroup, long expectedValue, CancellationToken cancellationToken)
    {
        var aggressiveSpinWait = new AggressiveSpinWait();
        long availableSequence;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            aggressiveSpinWait.SpinOnce();
            availableSequence = sequenceGroup.Value;
        }
        while (availableSequence < expectedValue);

        return availableSequence;
    }

    /// <summary>
    /// Waits until the sequence value is greater than or equal to the expected value using <see cref="SpinWait"/>.
    /// </summary>
    /// <returns>the sequence value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long SpinWaitFor(this DependentSequenceGroup sequenceGroup, long expectedValue, CancellationToken cancellationToken)
    {
        var availableSequence = sequenceGroup.Value;
        if (availableSequence >= expectedValue)
            return availableSequence;

        return SpinWaitForImpl(sequenceGroup, expectedValue, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long SpinWaitForImpl(DependentSequenceGroup sequenceGroup, long expectedValue, CancellationToken cancellationToken)
    {
        var spinWait = new SpinWait();
        long availableSequence;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            spinWait.SpinOnce();
            availableSequence = sequenceGroup.Value;
        }
        while (availableSequence < expectedValue);

        return availableSequence;
    }
}
