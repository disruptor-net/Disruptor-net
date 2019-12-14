using System;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using InlineIL;
using static InlineIL.IL.Emit;

namespace Disruptor.Benchmarks
{
    public class ValueArrayBenchmarks : IDisposable
    {
        private readonly Event[] _array;
        private GCHandle _handle;
        private IntPtr _pointer;

        public ValueArrayBenchmarks()
        {
            _array = new Event[1024];
            for (int i = 0; i < _array.Length; i++)
            {
                _array[i] = new Event { Value = i };
            }

            if (ReadOneIL() != ReadOne())
                throw new InvalidOperationException();

            _handle = GCHandle.Alloc(_array, GCHandleType.Pinned);
            _pointer = _handle.AddrOfPinnedObject();
        }

        public void Dispose()
        {
            _handle.Free();
        }

        public int Index = 371;

        [Benchmark(Baseline = true)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int ReadOne()
        {
            return ReadImpl<Event>(_array, Index).Value;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public Event ReadImplPublic(int index)
        {
            return ReadImpl<Event>(_array, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ref T ReadImpl<T>(object array, int index)
        {
            return ref ((T[])array)[index];
        }

        //[Benchmark]
        //[MethodImpl(MethodImplOptions.NoInlining)]
        //public int ReadOneUnsafe()
        //{
        //    return ReadUnsafeImpl<Event>(_array, Index).Value;
        //}

        //[MethodImpl(MethodImplOptions.NoInlining)]
        //public Event ReadUnsafeImplPublic(int index)
        //{
        //    return ReadUnsafeImpl<Event>(_array, index);
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private T ReadUnsafeImpl<T>(object array, int index)
        //{
        //    var typedArray = Unsafe.As<T[]>(array);
        //    ref var firstItem = ref Unsafe.As<object, T>(array[);
        //    return Unsafe.Add(ref firstItem, index);
        //}

        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int ReadOneIL()
        {
            return Util.ReadValue<Event>(_array, Index).Value;
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public unsafe int ReadOneILUnsafe()
        {
            return Util.ReadValue<Event>(_pointer, Index, sizeof(Event)).Value;
        }

        public struct Event
        {
            public int Value { get; set; }
        }
    }
}
