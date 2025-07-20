using System;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace Disruptor.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public unsafe class SequenceBenchmarks
{
    private readonly long[] _buffer = GC.AllocateArray<long>(64, pinned: true);
    private readonly Sequence _sequence = new(0);
    private readonly SequenceE1 _sequenceE1Value = new(0);
    private readonly SequenceE1 _sequenceE1Pointer;
    private readonly SequenceE2 _sequenceE2;
    private readonly SequenceE3 _sequenceE3;

    public SequenceBenchmarks()
    {
        _sequenceE1Pointer = new SequenceE1((long*)Unsafe.AsPointer(ref _buffer[8]));
        _sequenceE2 = new SequenceE2((long*)Unsafe.AsPointer(ref _buffer[16]));
        _sequenceE3 = new SequenceE3((long*)Unsafe.AsPointer(ref _buffer[24]));
    }

    [Benchmark, BenchmarkCategory("Add")]
    public long Add_Baseline()
    {
        return _sequence.AddAndGet(1);
    }

    [Benchmark, BenchmarkCategory("Add")]
    public long Add_E1_Value()
    {
        return _sequenceE1Value.AddAndGet(1);
    }

    [Benchmark, BenchmarkCategory("Add")]
    public long Add_E1_Pointer()
    {
        return _sequenceE1Pointer.AddAndGet(1);
    }

    [Benchmark, BenchmarkCategory("Add")]
    public long Add_E2()
    {
        return _sequenceE2.AddAndGet(1);
    }

    [Benchmark, BenchmarkCategory("Add")]
    public long Add_E3()
    {
        return _sequenceE3.AddAndGet(1);
    }

    [Benchmark, BenchmarkCategory("Increment")]
    public long Increment_Baseline()
    {
        return _sequence.IncrementAndGet();
    }

    [Benchmark, BenchmarkCategory("Increment")]
    public long Increment_E1_Value()
    {
        return _sequenceE1Value.IncrementAndGet();
    }

    [Benchmark, BenchmarkCategory("Increment")]
    public long Increment_E1_Pointer()
    {
        return _sequenceE1Pointer.IncrementAndGet();
    }

    [Benchmark, BenchmarkCategory("Increment")]
    public long Increment_E2()
    {
        return _sequenceE2.IncrementAndGet();
    }

    [Benchmark, BenchmarkCategory("Increment")]
    public long Increment_E3()
    {
        return _sequenceE3.IncrementAndGet();
    }

    [Benchmark, BenchmarkCategory("CompareAndSet")]
    public bool CompareAndSet_Baseline()
    {
        var value = _sequence.Value;
        return _sequence.CompareAndSet(value, value + 1);
    }

    [Benchmark, BenchmarkCategory("CompareAndSet")]
    public bool CompareAndSet_E1_Value()
    {
        var value = _sequenceE1Value.Value;
        return _sequenceE1Value.CompareAndSet(value, value + 1);
    }

    [Benchmark, BenchmarkCategory("CompareAndSet")]
    public bool CompareAndSet_E1_Pointer()
    {
        var value = _sequenceE1Pointer.Value;
        return _sequenceE1Pointer.CompareAndSet(value, value + 1);
    }

    [Benchmark, BenchmarkCategory("CompareAndSet")]
    public bool CompareAndSet_E2()
    {
        var value = _sequenceE2.Value;
        return _sequenceE2.CompareAndSet(value, value + 1);
    }

    [Benchmark, BenchmarkCategory("CompareAndSet")]
    public bool CompareAndSet_E3()
    {
        var value = _sequenceE3.Value;
        return _sequenceE3.CompareAndSet(value, value + 1);
    }

    [Benchmark(OperationsPerInvoke = 10_000), BenchmarkCategory("Read")]
    public long Read_Baseline()
    {
        var sum = 0L;
        for (var i = 0; i < 10_000; i++)
        {
            sum += _sequence.Value;
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = 10_000), BenchmarkCategory("Read")]
    public long Read_E1_Value()
    {
        var sum = 0L;
        for (var i = 0; i < 10_000; i++)
        {
            sum += _sequenceE1Value.Value;
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = 10_000), BenchmarkCategory("Read")]
    public long Read_E1_Pointer()
    {
        var sum = 0L;
        for (var i = 0; i < 10_000; i++)
        {
            sum += _sequenceE1Pointer.Value;
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = 10_000), BenchmarkCategory("Read")]
    public long Read_E2()
    {
        var sum = 0L;
        for (var i = 0; i < 10_000; i++)
        {
            sum += _sequenceE2.Value;
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = 10_000), BenchmarkCategory("Read")]
    public long Read_E3()
    {
        var sum = 0L;
        for (var i = 0; i < 10_000; i++)
        {
            sum += _sequenceE3.Value;
        }

        return sum;
    }
}
