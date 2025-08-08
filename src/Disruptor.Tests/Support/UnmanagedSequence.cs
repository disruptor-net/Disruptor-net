using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Disruptor.Tests.Support;

public unsafe class UnmanagedSequence
{
    private long* _value;

    /// <summary>
    /// Construct a new sequence counter that can be tracked across threads.
    /// </summary>
    /// <param name="initialValue">initial value for the counter</param>
    public UnmanagedSequence(long initialValue = Sequence.InitialCursorValue)
    {
        _value = PointerPool.Rent();
        *_value = initialValue;
    }

    ~UnmanagedSequence()
    {
        PointerPool.Return(_value);
    }

    internal SequencePointer Pointer => new(_value);

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

    public override string ToString()
    {
        return Value.ToString();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long IncrementAndGet()
    {
        return Interlocked.Increment(ref Unsafe.AsRef<long>(_value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long AddAndGet(long value)
    {
        return Interlocked.Add(ref Unsafe.AsRef<long>(_value), value);
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct SequenceBlock
    {
        [FieldOffset(0)]
        public long SequenceValue;
    }

    private static class PointerPool
    {
        private static int _nextPoolSize = 64;
        private static readonly List<GCHandle> _blockPools = new();
        private static readonly Stack<IntPtr> _availablePointers = new();
        private static readonly object _lock = new();

        static PointerPool()
        {
            Grow();
        }

        public static long* Rent()
        {
            lock (_lock)
            {
                if (_availablePointers.Count == 0)
                    Grow();

                return (long*)_availablePointers.Pop();
            }
        }

        public static void Return(long* pointer)
        {
            lock (_lock)
            {
                _availablePointers.Push((IntPtr)pointer);
            }
        }

        private static void Grow()
        {
            var poolSize = _nextPoolSize;
            var blockPool = GCHandle.Alloc(new SequenceBlock[poolSize], GCHandleType.Pinned);
            _blockPools.Add(blockPool);

            var poolPointer = blockPool.AddrOfPinnedObject();
            for (var i = 1; i < poolSize - 1; i++)
            {
                _availablePointers.Push(poolPointer + i * sizeof(SequenceBlock));
            }

            _nextPoolSize = poolSize * 2;
        }
    }
}
