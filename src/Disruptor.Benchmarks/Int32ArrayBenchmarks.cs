using BenchmarkDotNet.Attributes;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Disruptor.Benchmarks
{
    public unsafe class Int32ArrayBenchmarks
    {
        private int[] _array;
        private GCHandle _gcHandle;
        private int* _fixedArrayPointer;

        public Int32ArrayBenchmarks()
        {
            _array = new int[1024];
            _gcHandle = GCHandle.Alloc(new int[1024], GCHandleType.Pinned);
            _fixedArrayPointer = (int*)_gcHandle.AddrOfPinnedObject().ToPointer();
        }

        public int Index = 371;

        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Write()
        {
            _array[Index] = 777;
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void WriteFixed()
        {
            fixed (int* pointer = _array)
            {
                pointer[Index] = 888;
            }
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void WritePointer()
        {
            _fixedArrayPointer[Index] = 666;
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void WriteUnsafe()
        {
            Unsafe.Add(ref _array[0], Index) = 999;
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int Read()
        {
            return _array[Index];
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int ReadFixed()
        {
            fixed (int* pointer = _array)
            {
                return pointer[Index];
            }
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int ReadPointer()
        {
            return _fixedArrayPointer[Index];
        }
    }
}
