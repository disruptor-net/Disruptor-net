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

        public long Sequence { get; set; } = 75;

        [Benchmark(OperationsPerInvoke = 20)]
        public void SetValue_1()
        {
            for (var i = 0; i < 20; i++)
            {
                UseEvent(_ringBuffer[Sequence]);
            }
        }

        [Benchmark(OperationsPerInvoke = 20)]
        public void SetValue_2()
        {
            for (var i = 0; i < 20; i++)
            {
                UseEvent(_ringBuffer[Sequence]);
                UseEvent(_ringBuffer[Sequence + 1]);
            }
        }

        [Benchmark(OperationsPerInvoke = 20)]
        public void SetValue_5()
        {
            for (var i = 0; i < 20; i++)
            {
                UseEvent(_ringBuffer[Sequence]);
                UseEvent(_ringBuffer[Sequence + 1]);
                UseEvent(_ringBuffer[Sequence + 2]);
                UseEvent(_ringBuffer[Sequence + 3]);
                UseEvent(_ringBuffer[Sequence + 4]);
            }
        }

        [Benchmark(OperationsPerInvoke = 20)]
        public void SetValueSpan_1()
        {
            for (var i = 0; i < 20; i++)
            {
                var span = _ringBuffer[Sequence, Sequence];

                foreach (var data in span)
                {
                    UseEvent(data);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = 20)]
        public void SetValueSpan_2()
        {
            for (var i = 0; i < 20; i++)
            {
                var span = _ringBuffer[Sequence, Sequence + 1];

                foreach (var data in span)
                {
                    UseEvent(data);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = 20)]
        public void SetValueSpan_5()
        {
            for (var i = 0; i < 20; i++)
            {
                var span = _ringBuffer[Sequence, Sequence + 4];

                foreach (var data in span)
                {
                    UseEvent(data);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = 20)]
        public void SetValueBatch_1()
        {
            for (var i = 0; i < 20; i++)
            {
                var batch = _ringBuffer.GetBatch(Sequence, Sequence);

                foreach (var data in batch)
                {
                    UseEvent(data);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = 20)]
        public void SetValueBatch_2()
        {
            for (var i = 0; i < 20; i++)
            {
                var batch = _ringBuffer.GetBatch(Sequence, Sequence + 1);

                foreach (var data in batch)
                {
                    UseEvent(data);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = 20)]
        public void SetValueBatch_5()
        {
            for (var i = 0; i < 20; i++)
            {
                var batch = _ringBuffer.GetBatch(Sequence, Sequence + 4);

                foreach (var data in batch)
                {
                    UseEvent(data);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = 20)]
        public void SetValueBatchAsSpan_1()
        {
            for (var i = 0; i < 20; i++)
            {
                var batch = _ringBuffer.GetBatch(Sequence, Sequence);

                foreach (var data in batch.AsSpan())
                {
                    UseEvent(data);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = 20)]
        public void SetValueBatchAsSpan_2()
        {
            for (var i = 0; i < 20; i++)
            {
                var batch = _ringBuffer.GetBatch(Sequence, Sequence + 1);

                foreach (var data in batch.AsSpan())
                {
                    UseEvent(data);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = 20)]
        public void SetValueBatchAsSpan_5()
        {
            for (var i = 0; i < 20; i++)
            {
                var batch = _ringBuffer.GetBatch(Sequence, Sequence + 4);

                foreach (var data in batch.AsSpan())
                {
                    UseEvent(data);
                }
            }
        }

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
