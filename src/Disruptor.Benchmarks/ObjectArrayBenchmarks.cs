using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Disruptor.Benchmarks.Util;
using Disruptor.Util;

namespace Disruptor.Benchmarks;

[DisassemblyDiagnoser, ThroughputColumn]
public class ObjectArrayBenchmarks
{
    private readonly Event[] _array;
    private readonly int _mask;
    private int _index;

    public ObjectArrayBenchmarks()
    {
        _array = Enumerable.Range(0, 64)
                           .Select(i => new Event { Value = i })
                           .ToArray();

        _mask = _array.Length - 1;
    }

    [Benchmark(Baseline = true)]
    public Event ReadOneArray()
    {
        return _array[NextSequence()];
    }

    [Benchmark]
    public Event ReadOneIL()
    {
        return InternalUtil.Read<Event>(_array, NextSequence());
    }

    [Benchmark]
    public ReadOnlySpan<Event> ReadSpanIL()
    {
        var sequence = NextSequence();
        return InternalUtil.ReadSpan<Event>(_array, sequence, sequence);
    }

    private int NextSequence() => _index++ & _mask;

    public class Event
    {
        public int Value { get; set; }
    }
}