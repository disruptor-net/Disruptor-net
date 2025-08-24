using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Disruptor;

/// <summary>
/// Represents a group a sequences that are the dependencies for an IPC event processor.
/// </summary>
/// <remarks>
/// For a given event processors P, the dependencies are the sequences of the processors that must run before P.
/// If P is an initial processor of the disruptor processors graph, then the only dependency is the ring buffer cursor.
/// </remarks>
public class IpcDependentSequenceGroup
{
    private readonly SequencePointer _cursor;
    private readonly SequencePointer[] _dependencies;

    /// <summary>
    /// Creates a new dependent sequence group.
    /// </summary>
    /// <param name="cursor">The ring buffer cursor</param>
    /// <param name="dependencies">The sequences of the processors that must run before</param>
    public IpcDependentSequenceGroup(SequencePointer cursor, params SequencePointer[] dependencies)
    {
        _cursor = cursor;
        _dependencies = dependencies.Length == 0 ? [cursor] : dependencies;
    }

    /// <summary>
    /// Gets a value indicating whether the ring buffer cursor is the only dependency (i.e.: the event processors
    /// that use this <see cref="DependentSequenceGroup"/> are the first processors of the disruptor).
    /// </summary>
    public bool DependsOnCursor => _dependencies.Length == 1 && _dependencies[0] == _cursor;

    /// <summary>
    /// Gets the count of dependencies.
    /// </summary>
    public int DependentSequenceCount => _dependencies.Length;

    /// <summary>
    /// Gets the value of the ring buffer cursor.
    /// </summary>
    public long CursorValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _cursor.Value;
    }

    /// <summary>
    /// Gets the minimum value of the dependencies sequences.
    /// </summary>
    public long Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var minimum = long.MaxValue;
            foreach (var sequence in _dependencies)
            {
                var sequenceValue = sequence.Value;
                if (sequenceValue < minimum)
                    minimum = sequenceValue;
            }

            return minimum;
        }
    }

    /// <summary>
    /// Waits until the dependent sequences value is greater than or equal to the expected value using <see cref="AggressiveSpinWait"/>.
    /// </summary>
    /// <returns>the sequence value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long AggressiveSpinWaitFor(long expectedValue, CancellationToken cancellationToken)
    {
        var availableSequence = Value;
        if (availableSequence >= expectedValue)
            return availableSequence;

        return AggressiveSpinWaitForImpl(expectedValue, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private long AggressiveSpinWaitForImpl(long expectedValue, CancellationToken cancellationToken)
    {
        var aggressiveSpinWait = new AggressiveSpinWait();
        long availableSequence;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            aggressiveSpinWait.SpinOnce();
            availableSequence = Value;
        }
        while (availableSequence < expectedValue);

        return availableSequence;
    }

    /// <summary>
    /// Waits until the dependent sequences value is greater than or equal to the expected value using <see cref="SpinWait"/>.
    /// </summary>
    /// <returns>the sequence value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long SpinWaitFor(long expectedValue, CancellationToken cancellationToken)
    {
        var availableSequence = Value;
        if (availableSequence >= expectedValue)
            return availableSequence;

        return SpinWaitForImpl(expectedValue, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private long SpinWaitForImpl(long expectedValue, CancellationToken cancellationToken)
    {
        var spinWait = new SpinWait();
        long availableSequence;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            spinWait.SpinOnce();
            availableSequence = Value;
        }
        while (availableSequence < expectedValue);

        return availableSequence;
    }
}
