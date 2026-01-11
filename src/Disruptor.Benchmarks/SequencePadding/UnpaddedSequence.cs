using System.Runtime.CompilerServices;
using System.Threading;

namespace Disruptor.Benchmarks.SequencePadding;

public class UnpaddedSequence
{
    /// <summary>
    /// Set to -1 as sequence starting point
    /// </summary>
    public const long InitialCursorValue = -1;

    // padding: DefaultPadding

    private long _value;

    // padding: DefaultPadding

    /// <summary>
    /// Construct a new sequence counter that can be tracked across threads.
    /// </summary>
    /// <param name="initialValue">initial value for the counter</param>
    public UnpaddedSequence(long initialValue = InitialCursorValue)
    {
        _value = initialValue;
    }

    /// <summary>
    /// Gets the sequence value.
    /// </summary>
    /// <remarks>
    /// Performs a volatile read of the sequence value.
    /// </remarks>
    public long Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _value);
    }

    /// <summary>
    /// Sets the sequence value.
    /// </summary>
    /// <remarks>
    /// Performs an ordered write of this sequence. The intent is a Store/Store barrier between this write and any previous store.
    /// </remarks>
    /// <param name="value">The new value for the sequence.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetValue(long value)
    {
        Volatile.Write(ref _value, value);
    }
}
