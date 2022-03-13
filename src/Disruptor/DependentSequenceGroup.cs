using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Disruptor;

/// <summary>
/// Represents a group a sequences that are the dependencies for a set of event processors.
/// </summary>
/// <remarks>
/// For a given set of event processors S1, the dependencies are the sequences of the processors that must run before S1.
/// If S1 represents the first processors of the disruptor, then the only dependent sequence is the ring buffer cursor.
/// </remarks>
public class DependentSequenceGroup
{
    private readonly Sequence _cursor;
    private readonly Sequence[] _sequences;
    private readonly ISequence[] _untypedSequences;

    public DependentSequenceGroup(Sequence cursor, params ISequence[] dependentSequences)
    {
        _cursor = cursor;

        // The API exposes ISequence but the Disruptor code always uses the Sequence type for dependent sequences.
        // So dependent sequence are expected to be instances of Sequence in the fast path.

        // To simply the implementation, either all sequences are Sequence and _sequences is used,
        // or _untypedSequences is used.

        if (dependentSequences.Length == 0)
        {
            _sequences = new[] { cursor };
            _untypedSequences = Array.Empty<ISequence>();
        }
        else if (dependentSequences.All(x => x is Sequence))
        {
            _sequences = dependentSequences.Cast<Sequence>().ToArray();
            _untypedSequences = Array.Empty<ISequence>();
        }
        else
        {
            _sequences = Array.Empty<Sequence>();
            _untypedSequences = dependentSequences.ToArray();
        }
    }

    /// <summary>
    /// Gets the value of the ring buffer cursor.
    /// </summary>
    public long CursorValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _cursor.Value;
    }

    /// <summary>
    /// Gets the minimum value of the dependent sequences.
    /// </summary>
    public long Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_sequences.Length != 0)
            {
                var minimum = long.MaxValue;
                foreach (var sequence in _sequences)
                {
                    var sequenceValue = sequence.Value;
                    if (sequenceValue < minimum)
                        minimum = sequenceValue;
                }

                return minimum;
            }

            return GetValueFromUntypedSequences();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private long GetValueFromUntypedSequences()
    {
        var minimum = long.MaxValue;
        foreach (var sequence in _untypedSequences)
        {
            var sequenceValue = sequence.Value;
            if (sequenceValue < minimum)
                minimum = sequenceValue;
        }

        return minimum;
    }
}
