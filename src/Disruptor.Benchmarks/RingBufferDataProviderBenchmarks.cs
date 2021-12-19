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
            UseEvent(_ringBuffer[Index]);
        }

        [Benchmark]
        public void SetValue_2()
        {
            UseEvent(_ringBuffer[Index]);
            UseEvent(_ringBuffer[Index + 1]);
        }

#if BATCH_HANDLER
        [Benchmark]
        public void SetValueSpan_1()
        {
            var span = _ringBuffer[Index, Index];

            foreach (var data in span)
            {
                UseEvent(data);
            }
        }

        [Benchmark]
        public void SetValueSpan_2()
        {
            var span = _ringBuffer[Index, Index + 1];

            foreach (var data in span)
            {
                UseEvent(data);
            }
        }
#endif

        public class Event
        {
            public long Value { get; set; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void UseEvent(Event evt)
        {
            evt.Value = 42;
        }
    }
}
