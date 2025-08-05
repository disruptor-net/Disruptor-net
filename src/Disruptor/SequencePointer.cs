using System.Runtime.CompilerServices;
using System.Threading;

namespace Disruptor;

internal readonly unsafe struct SequencePointer
{
    private readonly long* _value;

    public SequencePointer(long* value)
    {
        _value = value;
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
        get => Volatile.Read(ref Unsafe.AsRef<long>(_value));
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
        Volatile.Write(ref Unsafe.AsRef<long>(_value), value);
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
        Volatile.Write(ref Unsafe.AsRef<long>(_value), value);
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
        return Interlocked.CompareExchange(ref Unsafe.AsRef<long>(_value), nextSequence, expectedSequence) == expectedSequence;
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
        return Interlocked.Increment(ref Unsafe.AsRef<long>(_value));
    }

    ///<summary>
    /// Atomically adds the supplied value.
    ///</summary>
    ///<returns>incremented sequence</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long AddAndGet(long value)
    {
        return Interlocked.Add(ref Unsafe.AsRef<long>(_value), value);
    }

    public bool PointerEquals(SequencePointer other)
    {
        return _value == other._value;
    }
}
