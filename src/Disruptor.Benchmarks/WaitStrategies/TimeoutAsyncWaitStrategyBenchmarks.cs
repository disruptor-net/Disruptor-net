using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Disruptor.Util;

namespace Disruptor.Benchmarks.WaitStrategies;

[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class TimeoutAsyncWaitStrategyBenchmarks : IDisposable
{
    private readonly IAsyncSequenceWaiter _waiter;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly IAsyncSequenceWaiter _waiterV1;
    // private readonly IDisposable _timerResolutionScope;

    public TimeoutAsyncWaitStrategyBenchmarks()
    {
        // _timerResolutionScope = TimerResolutionUtil.SetTimerResolution(1);

        var waitStrategy = new TimeoutAsyncWaitStrategy(TimeSpan.FromMilliseconds(1));
        _waiter = waitStrategy.NewAsyncSequenceWaiter(SequenceWaiterOwner.Unknown, new DependentSequenceGroup(new Sequence()));

        var waitStrategyV1 = new TimeoutAsyncWaitStrategyV1(TimeSpan.FromMilliseconds(1));
        _waiterV1 = waitStrategyV1.NewAsyncSequenceWaiter(SequenceWaiterOwner.Unknown, new DependentSequenceGroup(new Sequence()));

        _cancellationTokenSource = new CancellationTokenSource();
    }

    [Benchmark, BenchmarkCategory("Await1")]
    public ValueTask<SequenceWaitResult> Await1()
    {
        return _waiter.WaitForAsync(0, _cancellationTokenSource.Token);
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Await1")]
    public ValueTask<SequenceWaitResult> Await1_V1()
    {
        return _waiterV1.WaitForAsync(0, _cancellationTokenSource.Token);
    }

    // [Benchmark, BenchmarkCategory("Await2")]
    public ValueTask<SequenceWaitResult> Await2()
    {
        var result = _waiter.WaitForAsync(0, _cancellationTokenSource.Token);
        while (!result.IsCompleted)
        {
            Thread.Yield();
        }

        return result;
    }

    // [Benchmark(Baseline = true), BenchmarkCategory("Await2")]
    public ValueTask<SequenceWaitResult> Await2_V1()
    {
        var result = _waiterV1.WaitForAsync(0, _cancellationTokenSource.Token);
        while (!result.IsCompleted)
        {
            Thread.Yield();
        }

        return result;
    }

    // [Benchmark, BenchmarkCategory("Await3")]
    public ValueTask<SequenceWaitResult> Await3()
    {
        var result = _waiter.WaitForAsync(0, _cancellationTokenSource.Token);
        while (!result.IsCompleted)
        {
            Thread.Yield();
        }

        return ValueTask.FromResult(new SequenceWaitResult(0));
    }

    // [Benchmark(Baseline = true), BenchmarkCategory("Await3")]
    public ValueTask<SequenceWaitResult> Await3_V1()
    {
        var result = _waiterV1.WaitForAsync(0, _cancellationTokenSource.Token);
        while (!result.IsCompleted)
        {
            Thread.Yield();
        }

        return ValueTask.FromResult(new SequenceWaitResult(0));
    }

    // [Benchmark, BenchmarkCategory("Await4")]
    public void Await4()
    {
        var result = _waiter.WaitForAsync(0, _cancellationTokenSource.Token);
        while (!result.IsCompleted)
        {
            Thread.Yield();
        }
    }

    // [Benchmark(Baseline = true), BenchmarkCategory("Await4")]
    public void Await4_V1()
    {
        var result = _waiterV1.WaitForAsync(0, _cancellationTokenSource.Token);
        while (!result.IsCompleted)
        {
            Thread.Yield();
        }
    }

    public void Dispose()
    {
        _waiter.Dispose();
        _waiterV1.Dispose();
        // _timerResolutionScope.Dispose();
    }
}
