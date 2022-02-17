using System;
using BenchmarkDotNet.Attributes;
using Disruptor.Dsl;

namespace Disruptor.Benchmarks;

public class RingBufferReadBenchmarks : IDisposable
{
    private readonly RingBuffer<Event> _ringBuffer;
    private readonly ValueRingBuffer<ValueEvent> _valueRingBuffer;
    private readonly UnmanagedRingBuffer<ValueEvent> _unmanagedRingBuffer;
    private readonly UnmanagedRingBufferMemory _unmanagedRingBufferMemory;

    public RingBufferReadBenchmarks()
    {
        _ringBuffer = new RingBuffer<Event>(() => new Event(), new SingleProducerSequencer(4096, new BusySpinWaitStrategy()));
        _valueRingBuffer = new ValueRingBuffer<ValueEvent>(() => new ValueEvent(), new SingleProducerSequencer(4096, new BusySpinWaitStrategy()));
        _unmanagedRingBufferMemory = UnmanagedRingBufferMemory.Allocate(4096, () => new ValueEvent());
        _unmanagedRingBuffer = new UnmanagedRingBuffer<ValueEvent>(_unmanagedRingBufferMemory, ProducerType.Single, new BusySpinWaitStrategy());
    }

    public void Dispose()
    {
        _unmanagedRingBufferMemory.Dispose();
    }

    public long Sequence { get; set; } = 75;

    [Benchmark(OperationsPerInvoke = 100)]
    public long ReadOne()
    {
        var sum = 0L;

        for (var i = 0; i < 100; i++)
        {
            sum += _ringBuffer[Sequence].Value;
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = 100)]
    public long ReadSpan()
    {
        var sum = 0L;

        for (var i = 0; i < 100; i++)
        {
            sum += _ringBuffer[Sequence, Sequence][0].Value;
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = 100)]
    public long ReadValue()
    {
        var sum = 0L;

        for (var i = 0; i < 100; i++)
        {
            sum += _valueRingBuffer[Sequence].Value;
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = 100)]
    public long ReadUnmanaged()
    {
        var sum = 0L;

        for (var i = 0; i < 100; i++)
        {
            sum += _unmanagedRingBuffer[Sequence].Value;
        }

        return sum;
    }

    public class Event
    {
        public long Value { get; set; }
    }

    public struct ValueEvent
    {
        public long Value { get; set; }
    }
}
