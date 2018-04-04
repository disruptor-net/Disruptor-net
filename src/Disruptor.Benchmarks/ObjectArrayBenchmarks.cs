using System.Reflection.Emit;
using BenchmarkDotNet.Attributes;
using System.Runtime.CompilerServices;
using InlineIL;

namespace Disruptor.Benchmarks
{
    public class ObjectArrayBenchmarks
    {
        private static readonly int _offsetToArrayData = ElemOffset(new object[1]);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadILImpl<T>(int index)
        {
            IL.DeclareLocals(false, new LocalVar(typeof(byte).MakeByRefType()));
            IL.Emit(OpCodes.Ldarg_0);
            IL.Emit(OpCodes.Ldfld, new FieldRef(typeof(ObjectArrayBenchmarks), nameof(_array)));
            IL.Emit(OpCodes.Stloc_0);
            IL.Emit(OpCodes.Ldloc_0);

            IL.Emit(OpCodes.Ldarg_1);
            IL.Emit(OpCodes.Sizeof, typeof(object));
            IL.Emit(OpCodes.Mul);

            IL.Emit(OpCodes.Ldsfld, new FieldRef(typeof(ObjectArrayBenchmarks), nameof(_offsetToArrayData)));
            IL.Emit(OpCodes.Add);

            IL.Emit(OpCodes.Add);

            IL.Emit(OpCodes.Ldobj, typeof(T));

            return IL.Return<T>();
        }

        private static int ElemOffset<T>(T[] arr)
        {
            IL.Emit(OpCodes.Ldarg_0);
            IL.Emit(OpCodes.Ldc_I4_0);
            IL.Emit(OpCodes.Ldelema, typeof(T));
            IL.Emit(OpCodes.Ldarg_0);
            IL.Emit(OpCodes.Sub);

            return IL.Return<int>();
        }

        public class Event
        {
            public int Value { get; set; }
        }
    }
}
