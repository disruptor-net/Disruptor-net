using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using static Disruptor.Util.Constants;

namespace Disruptor.Benchmarks;

[StructLayout(LayoutKind.Explicit, Size = DefaultPadding * 2 + 16)]
public unsafe class SequenceE1
{
    /// <summary>
    /// Set to -1 as sequence starting point
    /// </summary>
    public const long InitialCursorValue = -1;

    // padding: DefaultPadding

    [FieldOffset(DefaultPadding)]
    private long _value;
    [FieldOffset(DefaultPadding + 8)]
    private long* _valuePointer;

    // padding: DefaultPadding

    /// <summary>
    /// Construct a new sequence counter that can be tracked across threads.
    /// </summary>
    /// <param name="initialValue">initial value for the counter</param>
    public SequenceE1(long initialValue = InitialCursorValue)
    {
        _value = initialValue;
    }

    public SequenceE1(long* valuePointer)
    {
        _valuePointer = valuePointer;
    }

    private ref long ValueRef
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _valuePointer == null ? ref _value : ref Unsafe.AsRef<long>(_valuePointer);
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
        get => Volatile.Read(ref ValueRef);
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

        Volatile.Write(ref ValueRef, value);
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
        Volatile.Write(ref ValueRef, value);
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
        return Interlocked.CompareExchange(ref ValueRef, nextSequence, expectedSequence) == expectedSequence;
    }

    /// <summary>
    /// Value of the <see cref="Sequence"/> as a String.
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
        return Interlocked.Increment(ref ValueRef);
    }

    ///<summary>
    /// Atomically adds the supplied value.
    ///</summary>
    ///<returns>incremented sequence</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long AddAndGet(long value)
    {
        return Interlocked.Add(ref ValueRef, value);
    }
}
