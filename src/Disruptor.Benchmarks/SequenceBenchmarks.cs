using System;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace Disruptor.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public unsafe class SequenceBenchmarks
{
    private readonly long[] _buffer = GC.AllocateArray<long>(64, pinned: true);
    private readonly SequenceE0 _sequence = new(0);
    private readonly ISequence _sequenceInterface = new SequenceE0(0);
    private readonly SequenceE1 _sequenceE1Value = new(0);
    private readonly SequenceE1 _sequenceE1Pointer;
    private readonly SequenceE2 _sequenceE2;
    private readonly SequenceE3 _sequenceE3;
    private readonly SequenceE0[] _sequenceArray;
    private readonly ISequence[] _sequenceInterfaceArray;
    private readonly SequenceE1[] _sequenceE1ValueArray;
    private readonly SequenceE1[] _sequenceE1PointerArray;
    private readonly SequenceE2[] _sequenceE2Array;
    private readonly SequenceE3[] _sequenceE3Array;

    public SequenceBenchmarks()
    {
        _sequenceE1Pointer = new SequenceE1((long*)Unsafe.AsPointer(ref _buffer[8]));
        _sequenceE2 = new SequenceE2((long*)Unsafe.AsPointer(ref _buffer[16]));
        _sequenceE3 = new SequenceE3((long*)Unsafe.AsPointer(ref _buffer[24]));

        _sequenceArray = [_sequence];
        _sequenceInterfaceArray = [_sequenceInterface];
        _sequenceE1ValueArray = [_sequenceE1Value];
        _sequenceE1PointerArray = [_sequenceE1Pointer];
        _sequenceE2Array = [_sequenceE2];
        _sequenceE3Array = [_sequenceE3];
    }

    [Benchmark, BenchmarkCategory("Add")]
    public long Add_Baseline()
    {
        return _sequence.AddAndGet(1);
    }

    [Benchmark, BenchmarkCategory("Add")]
    public long Add_Interface()
    {
        return _sequenceInterface.AddAndGet(1);
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
    public long Increment_Interface()
    {
        return _sequenceInterface.IncrementAndGet();
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
    public bool CompareAndSet_Interface()
    {
        var value = _sequenceInterface.Value;
        return _sequenceInterface.CompareAndSet(value, value + 1);
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
    public long Read_Interface()
    {
        var sum = 0L;
        for (var i = 0; i < 10_000; i++)
        {
            sum += _sequenceInterface.Value;
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

    [Benchmark(OperationsPerInvoke = 100), BenchmarkCategory("GetMinimum")]
    public long GetMinimum_Baseline()
    {
        var sum = 0L;
        for (var i = 0; i < 100; i++)
        {
            sum += GetMinimumSequence(_sequenceArray, _sequence.Value);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = 100), BenchmarkCategory("GetMinimum")]
    public long GetMinimum_Interface()
    {
        var sum = 0L;
        for (var i = 0; i < 100; i++)
        {
            sum += GetMinimumSequence(_sequenceInterfaceArray, _sequenceInterface.Value);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = 100), BenchmarkCategory("GetMinimum")]
    public long GetMinimum_E1_Value()
    {
        var sum = 0L;
        for (var i = 0; i < 100; i++)
        {
            sum += GetMinimumSequence(_sequenceE1ValueArray, _sequenceE1Value.Value);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = 100), BenchmarkCategory("GetMinimum")]
    public long GetMinimum_E1_Pointer()
    {
        var sum = 0L;
        for (var i = 0; i < 100; i++)
        {
            sum += GetMinimumSequence(_sequenceE1PointerArray, _sequenceE1Pointer.Value);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = 100), BenchmarkCategory("GetMinimum")]
    public long GetMinimum_E2()
    {
        var sum = 0L;
        for (var i = 0; i < 100; i++)
        {
            sum += GetMinimumSequence(_sequenceE2Array, _sequenceE2.Value);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = 100), BenchmarkCategory("GetMinimum")]
    public long GetMinimum_E3()
    {
        var sum = 0L;
        for (var i = 0; i < 100; i++)
        {
            sum += GetMinimumSequence(_sequenceE3Array, _sequenceE3.Value);
        }

        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long GetMinimumSequence(SequenceE0[] sequences, long minimum = long.MaxValue)
    {
        for (var i = 0; i < sequences.Length; i++)
        {
            var sequence = sequences[i].Value;
            if (sequence < minimum)
                minimum = sequence;
        }
        return minimum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long GetMinimumSequence(ISequence[] sequences, long minimum = long.MaxValue)
    {
        for (var i = 0; i < sequences.Length; i++)
        {
            var sequence = sequences[i].Value;
            if (sequence < minimum)
                minimum = sequence;
        }
        return minimum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long GetMinimumSequence(SequenceE1[] sequences, long minimum = long.MaxValue)
    {
        for (var i = 0; i < sequences.Length; i++)
        {
            var sequence = sequences[i].Value;
            if (sequence < minimum)
                minimum = sequence;
        }
        return minimum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long GetMinimumSequence(SequenceE2[] sequences, long minimum = long.MaxValue)
    {
        for (var i = 0; i < sequences.Length; i++)
        {
            var sequence = sequences[i].Value;
            if (sequence < minimum)
                minimum = sequence;
        }
        return minimum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long GetMinimumSequence(SequenceE3[] sequences, long minimum = long.MaxValue)
    {
        for (var i = 0; i < sequences.Length; i++)
        {
            var sequence = sequences[i].Value;
            if (sequence < minimum)
                minimum = sequence;
        }
        return minimum;
    }
}
