using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Disruptor.Benchmarks.WaitStrategies;

[MemoryDiagnoser]
public class PingPongAsyncWaitStrategyBenchmarks : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly AsyncWaitStrategy _pingWaitStrategy = new();
    private readonly AsyncWaitStrategy _pongWaitStrategy = new();
    private readonly Sequence _pingCursor = new();
    private readonly Sequence _pongCursor = new();
    private readonly Task _pongTask;
    private readonly IAsyncSequenceWaiter _pingSequenceWaiter;
    private readonly IAsyncSequenceWaiter _pongSequenceWaiter;

    public PingPongAsyncWaitStrategyBenchmarks()
    {
        _pingSequenceWaiter = _pingWaitStrategy.NewAsyncSequenceWaiter(null, new DependentSequenceGroup(_pingCursor));
        _pongSequenceWaiter = _pongWaitStrategy.NewAsyncSequenceWaiter(null, new DependentSequenceGroup(_pongCursor));
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

                await _pingSequenceWaiter.WaitForAsync(sequence, _cancellationTokenSource.Token).ConfigureAwait(false);

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
    public async Task Run()
    {
        var start = _pingCursor.Value + 1;
        var end = start + OperationsPerInvoke;

        for (var s = start; s < end; s++)
        {
            _pingCursor.SetValue(s);
            _pingWaitStrategy.SignalAllWhenBlocking();

            await _pongSequenceWaiter.WaitForAsync(s, _cancellationTokenSource.Token).ConfigureAwait(false);
        }
    }
}
