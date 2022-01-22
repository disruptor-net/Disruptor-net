using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Disruptor.Benchmarks.WaitStrategies;

[MemoryDiagnoser]
public class PingPongBlockingWaitStrategyBenchmarks : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly BlockingWaitStrategy _pingWaitStrategy;
    private readonly BlockingWaitStrategy _pongWaitStrategy;
    private readonly Sequence _pingCursor = new();
    private readonly Sequence _pongCursor = new();
    private readonly Task _pongTask;

    public PingPongBlockingWaitStrategyBenchmarks()
    {
        _pingWaitStrategy = new BlockingWaitStrategy();
        _pongWaitStrategy = new BlockingWaitStrategy();

        _pongTask = Task.Run(RunPong);
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _pongTask.Wait();
    }

    private void RunPong()
    {
        var sequence = -1L;

        try
        {
            while (true)
            {
                sequence++;

                _pingWaitStrategy.WaitFor(sequence, _pingCursor, _pingCursor, _cancellationTokenSource.Token);

                _pongCursor.SetValue(sequence);

                _pongWaitStrategy.SignalAllWhenBlocking();
            }
        }
        catch (OperationCanceledException e)
        {
            Console.WriteLine(e);
        }
    }

    [Benchmark(OperationsPerInvoke = 10_000_000)]
    public void Run()
    {
        var sequence = -1L;
        for (var i = 0; i < 10_000_000; i++)
        {
            sequence++;
            _pingCursor.SetValue(sequence);
            _pingWaitStrategy.SignalAllWhenBlocking();

            _pongWaitStrategy.WaitFor(sequence, _pongCursor, _pongCursor, _cancellationTokenSource.Token);
        }
    }
}
