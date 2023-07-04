using System;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace Disruptor.Benchmarks.Util;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class GetMinimumSequenceBenchmarks
{
    private readonly Sequence[] _sequencesSize1;
    private readonly Sequence[] _sequencesSize3;

    public GetMinimumSequenceBenchmarks()
    {
        _sequencesSize1 = new Sequence[]
        {
            new Sequence(1),
        };
        _sequencesSize3 = new Sequence[]
        {
            new Sequence(100),
            new Sequence(1),
            new Sequence(1000),
        };
    }

    [Benchmark, BenchmarkCategory("SizeOf1")]
    public long GetMinimumSequence1()
    {
        return UtilGetMinimumSequence(_sequencesSize1);
    }

    [Benchmark(Baseline = true), BenchmarkCategory("SizeOf1")]
    public long GetMinimumSequence1Ref()
    {
        return UtilGetMinimumSequenceRef(_sequencesSize1);
    }

    [Benchmark, BenchmarkCategory("SizeOf3")]
    public long GetMinimumSequence3()
    {
        return UtilGetMinimumSequence(_sequencesSize3);
    }

    [Benchmark(Baseline = true), BenchmarkCategory("SizeOf3")]
    public long GetMinimumSequence3Ref()
    {
        return UtilGetMinimumSequenceRef(_sequencesSize3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long UtilGetMinimumSequence(Sequence[] sequences, long minimum = long.MaxValue)
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
    public static long UtilGetMinimumSequenceRef(Sequence[] sequences, long minimum = long.MaxValue)
    {
        for (var i = 0; i < sequences.Length; i++)
        {
            var sequence = sequences[i].Value;
            minimum = Math.Min(minimum, sequence);
        }

        return minimum;
    }
}
