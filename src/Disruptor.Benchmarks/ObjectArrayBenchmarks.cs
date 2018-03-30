using BenchmarkDotNet.Attributes;
using System.Runtime.CompilerServices;

namespace Disruptor.Benchmarks
{
    public unsafe class ObjectArrayBenchmarks
    {
        private object[] _array;

        public ObjectArrayBenchmarks()
        {
            _array = new object[1024];
            for (int i = 0; i < _array.Length; i++)
            {
                _array[i] = new Event { Value = i };
            }
        }

        public int Index = 371;

        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int ReadOne()
        {
            return ReadImpl<Event>(Index).Value;
        }

        [Benchmark(OperationsPerInvoke = 1024)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int ReadMany()
        {
            var sum = 0;
            for (int i = 0; i < 1024; i++)
            {
                sum += ReadImpl<Event>(i).Value;
            }

            return sum;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T ReadImpl<T>(int index)
        {
            return (T)_array[index];
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int ReadOneUnsafe()
        {
            return ReadUnsafeImpl<Event>(Index).Value;
        }

        [Benchmark(OperationsPerInvoke = 1024)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int ReadManyUnsafe()
        {
            var sum = 0;
            for (int i = 0; i < 1024; i++)
            {
                sum += ReadUnsafeImpl<Event>(i).Value;
            }

            return sum;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T ReadUnsafeImpl<T>(int index)
        {
            ref var firstItem = ref Unsafe.As<object, T>(ref _array[0]);
            return Unsafe.Add(ref firstItem, index);
        }

        public class Event
        {
            public int Value { get; set; }
        }
    }
}
