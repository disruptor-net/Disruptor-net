using BenchmarkDotNet.Attributes;

namespace Disruptor.Benchmarks
{
    public class RingBufferBenchmarks
    {
        private readonly RingBuffer<TestEvent> _ringBuffer;

        public RingBufferBenchmarks()
        {
            _ringBuffer = new RingBuffer<TestEvent>(() => new TestEvent(), 4096);
        }

        [Benchmark(OperationsPerInvoke = 4096, Baseline = true)]
        public long Indexer()
        {
            var sum = 0L;

            for (var i = 0; i < 4096; i++)
            {
                sum += _ringBuffer[i].Data;
            }

            return sum;
        }

        public class TestEvent
        {
            public long Data { get; set; }
        }
    }
}
