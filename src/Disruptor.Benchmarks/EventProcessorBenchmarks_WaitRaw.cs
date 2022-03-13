using System;
using System.Runtime.CompilerServices;
using System.Threading;
using BenchmarkDotNet.Attributes;

namespace Disruptor.Benchmarks;

/// <summary>
/// Compares event processing loops to identify the cost of <c>if (waitResult.IsTimeout)</c>.
/// </summary>
internal class EventProcessorBenchmarks_WaitRaw
{
    private const int _operationsPerInvoke = 500;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Sequence _sequence = new();
    private readonly Sequence _cursor = new();
    private readonly YieldingWaitStrategy _waitStrategy = new();
    private readonly DependentSequenceGroup _dependentSequences;

    public EventProcessorBenchmarks_WaitRaw()
    {
        _dependentSequences = new DependentSequenceGroup(_cursor);
        _cursor.SetValue(42);
    }

    public int NextSequence { get; set; } = 42;

    [Benchmark(Baseline = true, OperationsPerInvoke = _operationsPerInvoke)]
    public void WaitWithCheck()
    {
        for (var i = 0; i < _operationsPerInvoke; i++)
        {
            var waitResult = _waitStrategy.WaitFor(NextSequence, _dependentSequences, _cancellationTokenSource.Token);
            if (waitResult.IsTimeout)
            {
                HandleTimeout(_sequence.Value);
                return;
            }

            var availableSequence = waitResult.UnsafeAvailableSequence;
            Process(availableSequence);

            _sequence.SetValue(availableSequence);
        }
    }

    [Benchmark(OperationsPerInvoke = _operationsPerInvoke)]
    public void WaitWithoutCheck()
    {
        for (var i = 0; i < _operationsPerInvoke; i++)
        {
            var waitResult = _waitStrategy.WaitFor(NextSequence, _dependentSequences, _cancellationTokenSource.Token);

            var availableSequence = waitResult.UnsafeAvailableSequence;
            Process(availableSequence);

            _sequence.SetValue(availableSequence);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void HandleTimeout(long sequence)
    {
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Process(long sequence)
    {
    }
}
