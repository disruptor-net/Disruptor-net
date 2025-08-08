using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using static Disruptor.Util.Constants;

namespace Disruptor;

/// <summary>
/// <para>Concurrent sequence class used for tracking the progress of
/// the ring buffer and event processors. Supports a number
/// of concurrent operations including CAS and order writes.
/// </para>
/// <para>
/// Also attempts to be more efficient in regard to false
/// sharing by adding padding around the volatile field.
/// </para>
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 8)]
public readonly unsafe struct SequencePointer : IEquatable<SequencePointer>
{
    [FieldOffset(0)]
    private readonly long* _value;

    public SequencePointer(long* value)
    {
        _value = value;
    }

    public long Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref Unsafe.AsRef<long>(_value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetValue(long value)
    {
        Volatile.Write(ref Unsafe.AsRef<long>(_value), value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetValueVolatile(long value)
    {
        Volatile.Write(ref Unsafe.AsRef<long>(_value), value);
        Thread.MemoryBarrier();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CompareAndSet(long expectedSequence, long nextSequence)
    {
        return Interlocked.CompareExchange(ref Unsafe.AsRef<long>(_value), nextSequence, expectedSequence) == expectedSequence;
    }

    public bool Equals(SequencePointer other)
    {
        return _value == other._value;
    }

    public override bool Equals(object? obj)
    {
        return obj is SequencePointer other && Equals(other);
    }

    public override int GetHashCode()
    {
        return (int)_value;
    }

    public static bool operator ==(SequencePointer left, SequencePointer right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(SequencePointer left, SequencePointer right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long AddAndGet(long value)
    {
        return Interlocked.Add(ref Unsafe.AsRef<long>(_value), value);
    }

    public static explicit operator long*(SequencePointer sequencePointer)
    {
        return sequencePointer._value;
    }
}
