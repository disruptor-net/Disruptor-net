using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Running;
using Disruptor.Benchmarks.WaitStrategies;
using ObjectLayoutInspector;

namespace Disruptor.Benchmarks;

public static class Program
{
    public static void Main()
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run();
        Console.ReadLine();
    }

    public static async Task MainTests()
    {
        //TypeLayout.PrintLayout<Sequence>();
        //TypeLayout.PrintLayout<RingBuffer<object>>();
        //TypeLayout.PrintLayout<ValueRingBuffer<ValueRingBufferBenchmarks.Event>.PublishScope>();
        //TypeLayout.PrintLayout<ValueRingBuffer<ValueRingBufferBenchmarks.Event>.PublishScopeRange>();

        await Task.Yield();
    }
}
