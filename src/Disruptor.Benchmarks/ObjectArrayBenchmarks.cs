using System.Linq;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace Disruptor.Benchmarks
{
    public class ObjectArrayBenchmarks
    {
        private readonly Event[] _array;

        public ObjectArrayBenchmarks()
        {
            _array = Enumerable.Range(0, 1024)
                               .Select(i => new Event { Value = i })
                               .ToArray();
        }

        public int Index = 371;

        [Benchmark(Baseline = true)]
        public int ReadOne()
        {
            return _array[Index].Value;
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int ReadOneIL()
        {
            return InternalUtil.Read<Event>(_array, Index).Value;
        }

        public class Event
        {
            public int Value { get; set; }
        }
    }
}
