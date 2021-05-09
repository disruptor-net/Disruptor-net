using BenchmarkDotNet.Attributes;

namespace Disruptor.Benchmarks
{
    public class SequenceBenchmarks
    {
        private readonly Sequence _sequence;

        public SequenceBenchmarks()
        {
            _sequence = new Sequence(0);
        }

        [Benchmark]
        public long Add()
        {
            return _sequence.AddAndGet(1);
        }

        [Benchmark]
        public long Increment()
        {
            return _sequence.IncrementAndGet();
        }

        [Benchmark]
        public bool CompareAndSet()
        {
            var value = _sequence.Value;
            return _sequence.CompareAndSet(value, value + 1);
        }
    }
}
