using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Disruptor.Benchmarks
{
    public class RingBufferDataProviderBenchmarks
    {
        private const int _batchSize = 10;

        private readonly RingBuffer<Event> _ringBuffer;

        public RingBufferDataProviderBenchmarks()
        {
            _ringBuffer = new RingBuffer<Event>(() => new Event(), new SingleProducerSequencer(4096, new BusySpinWaitStrategy()));
        }

        public int Index { get; set; } = 75;

        [Benchmark]
        public void SetValue()
        {
            _ringBuffer[Index].Value = 42;
        }

        [Benchmark(OperationsPerInvoke = _batchSize)]
        public void SetValueBatchConst()
        {
            var index = Index;
            var lo = index;
            var hi = index + _batchSize;
            for (var i = lo; i < hi; i++)
            {
                _ringBuffer[i].Value = 42;
            }
        }

        public class Event
        {
            public long Value { get; set; }
        }
    }
}
