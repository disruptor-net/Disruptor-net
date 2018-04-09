using BenchmarkDotNet.Attributes;
using System.Runtime.CompilerServices;

namespace Disruptor.Benchmarks
{
    public class RingBufferBenchmarks
    {
        private readonly RingBufferRef<TestEvent> _ringBuffer;

        public RingBufferBenchmarks()
        {
            _ringBuffer = new RingBufferRef<TestEvent>(() => new TestEvent(), 4096);
        }

        public int Index = 371;

        [Benchmark(Baseline = true)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public TestEvent Indexer()
        {
            return _ringBuffer[Index];
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public TestEvent IndexerUnsafe()
        {
            return _ringBuffer.GetUnsafe(Index);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public long IndexerUnsafeSum()
        {
            var sum = 0L;
            sum += _ringBuffer.GetUnsafe(Index).Data;

            return sum;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public long IndexerUnsafeSumForLoop()
        {
            var sum = 0L;
            for (int i = 0; i < Index; i++)
            {
                sum += _ringBuffer.GetUnsafe(Index).Data;
            }

            return sum;
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public TestEvent IndexerArray()
        {
            return _ringBuffer.GetArray(Index);
        }

        public class TestEvent
        {
            public long Data { get; set; }
        }
    }
}
