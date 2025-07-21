using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Disruptor.Util;

namespace Disruptor.Benchmarks;

public interface ISequence
{
    /// <summary>
    /// Gets the sequence value.
    /// </summary>
    /// <remarks>
    /// Performs a volatile read of the sequence value.
    /// </remarks>
    long Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }

    /// <summary>
    /// Sets the sequence value.
    /// </summary>
    /// <remarks>
    /// Performs an ordered write of this sequence. The intent is a Store/Store barrier between this write and any previous store.
    /// </remarks>
    /// <param name="value">The new value for the sequence.</param>
    void SetValue(long value);

    /// <summary>
    /// Sets the sequence value.
    /// </summary>
    /// <remarks>
    /// Performs a volatile write of this sequence. The intent is a Store/Store barrier between this write and any previous write
    /// and a Store/Load barrier between this write and any subsequent volatile read.
    /// </remarks>
    /// <param name="value">The new value for the sequence.</param>
    void SetValueVolatile(long value);

    /// <summary>
    /// Performs a compare and set operation on the sequence.
    /// </summary>
    /// <param name="expectedSequence">the expected value for the sequence</param>
    /// <param name="nextSequence">the new value for the sequence</param>
    /// <returns>true if the operation succeeds, false otherwise.</returns>
    bool CompareAndSet(long expectedSequence, long nextSequence);

    ///<summary>
    /// Atomically increments the sequence by one.
    ///</summary>
    ///<returns>incremented sequence</returns>
    long IncrementAndGet();

    ///<summary>
    /// Atomically adds the supplied value.
    ///</summary>
    ///<returns>incremented sequence</returns>
    long AddAndGet(long value);
}

[StructLayout(LayoutKind.Explicit, Size = Constants.DefaultPadding * 2 + 8)]
public class SequenceE0 : ISequence
{
    /// <summary>
    /// Set to -1 as sequence starting point
    /// </summary>
    public const long InitialCursorValue = -1;

    // padding: DefaultPadding

    [FieldOffset(Constants.DefaultPadding)]
    private long _value;

    // padding: DefaultPadding

    /// <summary>
    /// Construct a new sequence counter that can be tracked across threads.
    /// </summary>
    /// <param name="initialValue">initial value for the counter</param>
    public SequenceE0(long initialValue = InitialCursorValue)
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

    /// <summary>
    /// Sets the sequence value.
    /// </summary>
    /// <remarks>
    /// Performs a volatile write of this sequence. The intent is a Store/Store barrier between this write and any previous write
    /// and a Store/Load barrier between this write and any subsequent volatile read.
    /// </remarks>
    /// <param name="value">The new value for the sequence.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetValueVolatile(long value)
    {
        Volatile.Write(ref _value, value);
        Thread.MemoryBarrier();
    }

    /// <summary>
    /// Performs a compare and set operation on the sequence.
    /// </summary>
    /// <param name="expectedSequence">the expected value for the sequence</param>
    /// <param name="nextSequence">the new value for the sequence</param>
    /// <returns>true if the operation succeeds, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CompareAndSet(long expectedSequence, long nextSequence)
    {
        return Interlocked.CompareExchange(ref _value, nextSequence, expectedSequence) == expectedSequence;
    }

    /// <summary>
    /// Value of the <see cref="Disruptor.Sequence"/> as a String.
    /// </summary>
    /// <returns>String representation of the sequence.</returns>
    public override string ToString()
    {
        return Value.ToString();
    }

    ///<summary>
    /// Atomically increments the sequence by one.
    ///</summary>
    ///<returns>incremented sequence</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long IncrementAndGet()
    {
        return Interlocked.Increment(ref _value);
    }

    ///<summary>
    /// Atomically adds the supplied value.
    ///</summary>
    ///<returns>incremented sequence</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long AddAndGet(long value)
    {
        return Interlocked.Add(ref _value, value);
    }
}
