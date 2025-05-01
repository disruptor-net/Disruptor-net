using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Disruptor.Benchmarks.WaitStrategies;

[MemoryDiagnoser]
public class PingPongYieldingWaitStrategyBenchmarks : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly YieldingWaitStrategy _pingWaitStrategy = new();
    private readonly YieldingWaitStrategy _pongWaitStrategy = new();
    private readonly Sequence _pingCursor = new();
    private readonly Sequence _pongCursor = new();
    private readonly ManualResetEventSlim _pongStarted = new();
    private readonly Task _pongTask;
    private readonly ISequenceWaiter _pingSequenceWaiter;
    private readonly ISequenceWaiter _pongSequenceWaiter;

    public PingPongYieldingWaitStrategyBenchmarks()
    {
        _pingSequenceWaiter = _pingWaitStrategy.NewSequenceWaiter(null, new DependentSequenceGroup(_pingCursor));
        _pongSequenceWaiter = _pongWaitStrategy.NewSequenceWaiter(null, new DependentSequenceGroup(_pongCursor));
        _pongTask = Task.Run(RunPong);
        _pongStarted.Wait();
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _pongTask.Wait();
    }

    private void RunPong()
    {
        _pongStarted.Set();

        try
        {
            var sequence = -1L;

            while (true)
            {
                sequence++;

                _pingSequenceWaiter.WaitFor(sequence, _cancellationTokenSource.Token);

                _pongCursor.SetValue(sequence);
                _pongWaitStrategy.SignalAllWhenBlocking();
            }
        }
        catch (OperationCanceledException e)
        {
            Console.WriteLine(e);
        }
    }

    public const int OperationsPerInvoke = 1_000_000;

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Run()
    {
        var start = _pingCursor.Value + 1;
        var end = start + OperationsPerInvoke;

        for (var s = start; s < end; s++)
        {
            _pingCursor.SetValue(s);
            _pingWaitStrategy.SignalAllWhenBlocking();

            _pongSequenceWaiter.WaitFor(s, _cancellationTokenSource.Token);
        }
    }
}
