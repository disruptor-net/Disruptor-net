using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Disruptor;

/// <summary>
/// Represents a group a sequences that are the dependencies for an event processor.
/// </summary>
/// <remarks>
/// For a given event processor P, the dependencies are the sequences of the processors that must run before P.
/// If P is an initial processor of the disruptor processors graph, then the only dependency is the ring buffer cursor.
/// </remarks>
public class DependentSequenceGroup
{
    // ReSharper disable NotAccessedField.Local
    private readonly Sequence _cursor;
    private readonly Sequence[] _dependencies;
    // ReSharper restore NotAccessedField.Local
    private readonly SequencePointer _cursorPointer;
    private readonly SequencePointer[] _dependencyPointers;
    private object? _tag;

    /// <summary>
    /// Creates a new dependent sequence group.
    /// </summary>
    /// <param name="cursor">The ring buffer cursor</param>
    /// <param name="dependencies">The sequences of the processors that must run before</param>
    public DependentSequenceGroup(Sequence cursor, params Sequence[] dependencies)
    {
        _cursor = cursor;
        _dependencies = dependencies;

        _cursorPointer = cursor.GetPointer();
        _dependencyPointers = dependencies.Length == 0 ? [cursor.GetPointer()] : dependencies.Select(x => x.GetPointer()).ToArray();
    }

    /// <summary>
    /// Gets a value indicating whether the ring buffer cursor is the only dependency (i.e.: the event processors
    /// that use this <see cref="DependentSequenceGroup"/> are the first processors of the disruptor).
    /// </summary>
    public bool DependsOnCursor => _dependencyPointers.Length == 1 && _dependencyPointers[0].PointerEquals(_cursorPointer);

    /// <summary>
    /// Gets the count of dependencies.
    /// </summary>
    public int DependentSequenceCount => _dependencyPointers.Length;

    /// <summary>
    /// Gets the value of the ring buffer cursor.
    /// </summary>
    public long CursorValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _cursorPointer.Value;
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
            foreach (var sequence in _dependencyPointers)
            {
                var sequenceValue = sequence.Value;
                if (sequenceValue < minimum)
                    minimum = sequenceValue;
            }

            return minimum;
        }
    }

    /// <summary>
    /// Gets or sets a user-defined tag object attached to the <see cref="DependentSequenceGroup"/>.
    /// </summary>
    /// <remarks>
    /// The tag can be used to apply custom logic in wait strategies.
    /// </remarks>
    public object? Tag
    {
        get => _tag;
        set => _tag = value;
    }

    /// <summary>
    /// Indicates whether the target <see cref="DependentSequenceGroup"/> has the same dependent sequences
    /// as the current instance.
    /// </summary>
    public bool HasSameDependencies(DependentSequenceGroup dependentSequenceGroup)
    {
        if (!_cursorPointer.PointerEquals(dependentSequenceGroup._cursorPointer))
            return false;

        if (_dependencyPointers.Length != dependentSequenceGroup._dependencyPointers.Length)
            return false;

        for (var index = 0; index < _dependencyPointers.Length; index++)
        {
            if (!_dependencyPointers[index].PointerEquals(dependentSequenceGroup._dependencyPointers[index]))
                return false;
        }

        return true;
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
