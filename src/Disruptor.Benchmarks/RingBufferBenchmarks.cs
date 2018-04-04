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

        public int Index = 371;

        [Benchmark(Baseline = true)]
        public TestEvent Indexer()
        {
            return _ringBuffer[Index];
        }

        public class TestEvent
        {
            public long Data { get; set; }
        }
    }
}
