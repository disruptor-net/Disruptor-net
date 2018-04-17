using System;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using InlineIL;
using static InlineIL.IL.Emit;

namespace Disruptor.Benchmarks
{
    public class ObjectArrayBenchmarks
    {
        private static readonly int _offsetToArrayData = ElemOffset(new object[1]);
        private readonly object[] _array;

        public ObjectArrayBenchmarks()
        {
            _array = new object[1024];
            for (int i = 0; i < _array.Length; i++)
            {
                _array[i] = new Event { Value = i };
            }

            var item = _array[42];

            if (!ReferenceEquals(ReadILImpl<Event>(42), item))
                throw new InvalidOperationException();

            if (!ReferenceEquals(ReadILImpl2<Event>(42), item))
                throw new InvalidOperationException();
        }

        public int Index = 371;

        [Benchmark(Baseline = true)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int ReadOne()
        {
            return ReadImpl<Event>(Index).Value;
        }

        //[Benchmark(OperationsPerInvoke = 1024)]
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        public Event ReadImplPublic(int index)
        {
            return ReadImpl<Event>(index);
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

        //[Benchmark(OperationsPerInvoke = 1024)]
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        public Event ReadUnsafeImplPublic(int index)
        {
            return ReadUnsafeImpl<Event>(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T ReadUnsafeImpl<T>(int index)
        {
            ref var firstItem = ref Unsafe.As<object, T>(ref _array[0]);
            return Unsafe.Add(ref firstItem, index);
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int ReadOneIL()
        {
            return ReadILImpl<Event>(Index).Value;
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int ReadOneIL2()
        {
            return ReadILImpl2<Event>(Index).Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadILImpl<T>(int index)
        {
            IL.DeclareLocals(
                false,
                typeof(byte).MakeByRefType()
            );

            Ldarg_0();
            Ldfld(new FieldRef(typeof(ObjectArrayBenchmarks), nameof(_array)));
            Stloc_0();
            Ldloc_0();

            Ldarg(nameof(index));
            Sizeof(typeof(object));
            Mul();

            Ldsfld(new FieldRef(typeof(ObjectArrayBenchmarks), nameof(_offsetToArrayData)));
            Add();

            Add();

            Ldobj(typeof(T));

            return IL.Return<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadILImpl2<T>(int index)
        {
            Ldarg_0();
            Ldfld(new FieldRef(typeof(ObjectArrayBenchmarks), nameof(_array)));

            Ldarg(nameof(index));
            Readonly(); // Trigger this codepath in the JIT: https://github.com/dotnet/coreclr/blob/bc28740cd5f0533655f347fc315f6a28836a7efe/src/jit/importer.cpp#L11141-L11147
            Ldelema(typeof(T));
            Ldind_Ref();

            return IL.Return<T>();
        }

        private static int ElemOffset<T>(T[] arr)
        {
            Ldarg(nameof(arr));
            Ldc_I4_0();
            Ldelema(typeof(T));
            Ldarg(nameof(arr));
            Sub();

            return IL.Return<int>();
        }

        public class Event
        {
            public int Value { get; set; }
        }
    }
}
