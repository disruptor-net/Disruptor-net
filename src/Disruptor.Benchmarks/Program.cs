using BenchmarkDotNet.Running;

namespace Disruptor.Benchmarks
{
    public static class Program
    {
        public static void Main()
        {
            BenchmarkRunner.Run<RingBufferBenchmarks>();
        }
    }
}
