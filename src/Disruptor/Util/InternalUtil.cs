using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using InlineIL;
using static InlineIL.IL.Emit;

namespace Disruptor.Util;

internal static class InternalUtil
{
    /// <summary>
    /// Ring buffer padding size in bytes.
    ///
    /// The padding should be added at the beginning and at the end of the
    /// ring buffer arrays.
    ///
    /// Used to avoid false sharing.
    /// </summary>
    public const int RingBufferPaddingBytes = 128;

    /// <summary>
    /// Gets the ring buffer padding as a number of events.
    /// </summary>
    /// <param name="eventSize"></param>
    /// <returns></returns>
    public static int GetRingBufferPaddingEventCount(int eventSize)
    {
        return (int)Math.Ceiling((double)RingBufferPaddingBytes / eventSize);
    }

    // +----------+-----------------+--------------------+
    // | Runtime  | ArrayDataOffset | OffsetToStringData |
    // +----------+-----------------+--------------------+
    // | Core-x32 |               8 |                  8 |
    // | Core-x64 |              16 |                 12 |
    // | Mono-x32 |              16 |                 12 |
    // | Mono-x64 |              32 |                 20 |
    // +----------+-----------------+--------------------+

#pragma warning disable 618
    public static unsafe int ArrayDataOffset
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => sizeof(IntPtr) == 4
            ? RuntimeHelpers.OffsetToStringData == 8 ? 8 : 16
            : RuntimeHelpers.OffsetToStringData == 12
                ? 16
                : 32;
    }
#pragma warning restore 618

#if NET
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Read<T>(object array, int index)
        => Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Unsafe.As<T[]>(array)), (uint)index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Read<T>(object array, long index)
        => Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Unsafe.As<T[]>(array)), (nint)index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> ReadSpan<T>(object? array, int index, int length)
        => array is null ? default : MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Unsafe.As<T[]>(array)), (uint)index), length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T ReadValue<T>(object array, int index)
        => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Unsafe.As<T[]>(array)), (uint)index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref T ReadValue<T>(IntPtr pointer, int index, int size)
        where T : struct
        => ref Unsafe.AddByteOffset(ref Unsafe.AsRef<T>((void*)pointer), (uint)(index * size));

#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Read<T>(object array, int index)
        where T : class
    {
        IL.DeclareLocals(false, typeof(byte).MakeByRefType());

        Ldarg(nameof(array));
        Stloc_0(); // convert the object pointer to a byref
        Ldloc_0(); // load the object pointer as a byref

        Ldarg(nameof(index));
        Conv_U(); // zero extend

        Sizeof(typeof(object));
        Mul(); // index x sizeof(object)

        Call(MethodRef.PropertyGet(typeof(InternalUtil), nameof(ArrayDataOffset)));
        Add(); // index x sizeof(object) +  ArrayDataOffset

        Add(); // array + index x sizeof(object) + ArrayDataOffset

        Ldobj(typeof(T)); // load a T value from the computed address

        return IL.Return<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> ReadSpan<T>(object array, int index, int length)
        where T : class
    {
        IL.DeclareLocals(false, typeof(byte).MakeByRefType());

        Ldarg(nameof(array));
        Stloc_0(); // convert the object pointer to a byref
        Ldloc_0(); // load the object pointer as a byref

        Ldarg(nameof(index));
        Sizeof(typeof(object));
        Mul(); // index x sizeof(object)

        Call(MethodRef.PropertyGet(typeof(InternalUtil), nameof(ArrayDataOffset)));
        Add(); // index x sizeof(object) +  ArrayDataOffset

        Add(); // array + index x sizeof(object) + ArrayDataOffset

        Ldarg(nameof(length));
        Call(MethodRef.Method(typeof(MemoryMarshal), nameof(MemoryMarshal.CreateReadOnlySpan), typeof(T).MakeByRefType(), typeof(int)).MakeGenericMethod(typeof(T)));
        Ret();

        throw IL.Unreachable();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T ReadValue<T>(object array, int index)
        where T : struct
    {
        IL.DeclareLocals(false, typeof(byte).MakeByRefType());

        Ldarg(nameof(array));
        Stloc_0(); // convert the object pointer to a byref
        Ldloc_0(); // load the object pointer as a byref

        Ldarg(nameof(index));
        Conv_U(); // zero extend

        Sizeof(typeof(T));
        Mul(); // index x sizeof(T)

        Call(MethodRef.PropertyGet(typeof(InternalUtil), nameof(ArrayDataOffset)));
        Add(); // index x sizeof(T) +  ArrayDataOffset

        Add(); // array + index x sizeof(T) +  ArrayDataOffset

        Ret();

        throw IL.Unreachable();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T ReadValue<T>(IntPtr pointer, int index, int size)
        where T : struct
    {
        IL.DeclareLocals(false, typeof(byte).MakeByRefType());

        Ldarg(nameof(pointer));
        Stloc_0(); // convert the object pointer to a byref
        Ldloc_0(); // load the object pointer as a byref

        Ldarg(nameof(index));
        Ldarg(nameof(size));

        Mul(); // index x size
        Conv_U(); // zero extend

        Add(); // pointer + index x size

        Ret();

        throw IL.Unreachable();
    }
#endif

    public static int SizeOf<T>()
        where T : struct
    {
        Sizeof(typeof(T));

        return IL.Return<int>();
    }

    public static int ComputeArrayDataOffset()
    {
        var array = new object[1];

        return (int)GetElementOffset(array, ref array[0]);
    }

    private static IntPtr GetElementOffset(object origin, ref object target)
    {
        IL.DeclareLocals(false, typeof(byte).MakeByRefType());

        Ldarg(nameof(target));

        Ldarg(nameof(origin)); // load the object
        Stloc_0(); // convert the object pointer to a byref
        Ldloc_0(); // load the object pointer as a byref

        Sub();

        return IL.Return<IntPtr>();
    }
}
