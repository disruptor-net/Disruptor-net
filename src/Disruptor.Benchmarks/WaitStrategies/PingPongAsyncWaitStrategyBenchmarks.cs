using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Disruptor.Benchmarks.WaitStrategies;

[MemoryDiagnoser]
public class PingPongAsyncWaitStrategyBenchmarks : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly AsyncWaitStrategy _pingWaitStrategy;
    private readonly AsyncWaitStrategy _pongWaitStrategy;
    private readonly Sequence _pingCursor = new();
    private readonly Sequence _pongCursor = new();
    private readonly Task _pongTask;

    public PingPongAsyncWaitStrategyBenchmarks()
    {
        _pingWaitStrategy = new AsyncWaitStrategy();
        _pongWaitStrategy = new AsyncWaitStrategy();

        _pongTask = Task.Run(RunPong);
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _pongTask.Wait();
    }

    private async Task RunPong()
    {
        var sequence = -1L;

        try
        {
            while (true)
            {
                sequence++;

                await _pingWaitStrategy.WaitForAsync(sequence, _pingCursor, _pingCursor, _cancellationTokenSource.Token).ConfigureAwait(false);

                _pongCursor.SetValue(sequence);

                _pongWaitStrategy.SignalAllWhenBlocking();
            }
        }
        catch (OperationCanceledException e)
        {
            Console.WriteLine(e);
        }
    }

    [Benchmark(OperationsPerInvoke = 1_000_000)]
    public async Task Run()
    {
        var sequence = -1L;
        for (var i = 0; i < 1_000_000; i++)
        {
            sequence++;
            _pingCursor.SetValue(sequence);
            _pingWaitStrategy.SignalAllWhenBlocking();

            await _pongWaitStrategy.WaitForAsync(sequence, _pongCursor, _pongCursor, _cancellationTokenSource.Token).ConfigureAwait(false);
        }
    }
}
