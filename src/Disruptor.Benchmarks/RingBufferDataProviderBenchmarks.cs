using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Disruptor.Benchmarks
{
    public class RingBufferDataProviderBenchmarks
    {
        private readonly RingBuffer<Event> _ringBuffer;

        public RingBufferDataProviderBenchmarks()
        {
            _ringBuffer = new RingBuffer<Event>(() => new Event(), new SingleProducerSequencer(4096, new BusySpinWaitStrategy()));
        }

        public int Index { get; set; } = 75;

        [Benchmark]
        public void SetValue_1()
        {
            _ringBuffer[Index].Value = 42;
        }

        [Benchmark]
        public void SetValue_2()
        {
            _ringBuffer[Index].Value = 42;
            _ringBuffer[Index + 1].Value = 42;
        }

#if NETCOREAPP
        [Benchmark]
        public void SetValueSpan_1()
        {
            var span = _ringBuffer[Index, Index];

            foreach (var data in span)
            {
                data.Value = 42;
            }
        }

        [Benchmark]
        public void SetValueSpan_2()
        {
            var span = _ringBuffer[Index, Index + 1];

            foreach (var data in span)
            {
                data.Value = 42;
            }
        }
#endif

        public class Event
        {
            public long Value { get; set; }
        }
    }
}
