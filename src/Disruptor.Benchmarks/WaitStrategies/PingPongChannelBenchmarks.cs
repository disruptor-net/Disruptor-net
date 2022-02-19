using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Disruptor.Benchmarks.WaitStrategies;

[MemoryDiagnoser]
public class PingPongChannelBenchmarks : IDisposable
{
    private const int _bufferSize = 1024;
    private readonly Event _event = new();
    private readonly Channel<Event> _pingChannel;
    private readonly Channel<Event> _pongChannel;
    private readonly Task _pongTask;

    public PingPongChannelBenchmarks()
    {
        _pingChannel = Channel.CreateBounded<Event>(new BoundedChannelOptions(_bufferSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
        });
        _pongChannel = Channel.CreateBounded<Event>(new BoundedChannelOptions(_bufferSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
        });

        _pongTask = Task.Run(RunPong);
    }

    public void Dispose()
    {
        _pingChannel.Writer.Complete();
        _pongTask.Wait();
    }

    private async Task RunPong()
    {
        while (await _pingChannel.Reader.WaitToReadAsync().ConfigureAwait(false))
        {
            while (_pingChannel.Reader.TryRead(out _))
            {
                await _pongChannel.Writer.WriteAsync(_event).ConfigureAwait(false);
            }
        }
    }

    [Benchmark(OperationsPerInvoke = 1_000_000)]
    public async Task Run()
    {
        for (var i = 0; i < 1_000_000; i++)
        {
            await _pingChannel.Writer.WriteAsync(_event).ConfigureAwait(false);
            await _pongChannel.Reader.ReadAsync().ConfigureAwait(false);
        }
    }

    public class Event
    {
    }
}
