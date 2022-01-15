using System;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace Disruptor.Benchmarks;

[DisassemblyDiagnoser]
public class RingBufferBatchingBenchmarks
{
    private readonly RingBuffer<Event> _ringBuffer;

    public RingBufferBatchingBenchmarks()
    {
        _ringBuffer = new RingBuffer<Event>(() => new Event(), new SingleProducerSequencer(4096, new BusySpinWaitStrategy()));
    }

    public long Sequence { get; set; } = 75;

    [Params(1, 2, 5, 10)]
    public int BatchSize { get; set; }

    [Benchmark(OperationsPerInvoke = 20)]
    public void SetValue()
    {
        for (var i = 0; i < 20; i++)
        {
            var sequence = Sequence;
            var batchSize = BatchSize;
            for (var j = 0; j < batchSize; j++)
            {
                UseEvent(_ringBuffer[sequence + j], sequence + j);
            }
        }
    }

    [Benchmark(OperationsPerInvoke = 20)]
    public void SetValueSpan()
    {
        for (var i = 0; i < 20; i++)
        {
            var sequence = Sequence;
            var span = _ringBuffer[sequence, sequence + BatchSize - 1];

            UseEvent(span, sequence);
        }
    }

    [Benchmark(OperationsPerInvoke = 20)]
    public void SetValueBatch()
    {
        for (var i = 0; i < 20; i++)
        {
            var sequence = Sequence;
            var batch = _ringBuffer.GetBatch(sequence, sequence + BatchSize - 1);

            UseEvent(batch, sequence);
        }
    }

    [Benchmark(OperationsPerInvoke = 20)]
    public void SetValueBatchAsSpan()
    {
        for (var i = 0; i < 20; i++)
        {
            var sequence = Sequence;
            var batch = _ringBuffer.GetBatch(sequence, sequence + BatchSize - 1);

            UseEventAsSpan(batch, sequence);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void UseEvent(EventBatch<Event> batch, long sequence)
    {
        foreach (var evt in batch)
        {
            evt.Value = 42;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void UseEventAsSpan(EventBatch<Event> batch, long sequence)
    {
        foreach (var evt in batch.AsSpan())
        {
            evt.Value = 42;
        }
    }

    public class Event
    {
        public long Value { get; set; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void UseEvent(Event evt, long sequence)
    {
        evt.Value = 42;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void UseEvent(ReadOnlySpan<Event> batch, long sequence)
    {
        foreach (var evt in batch)
        {
            evt.Value = 42;
        }
    }
}