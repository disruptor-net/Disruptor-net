using System;
using BenchmarkDotNet.Running;
using ObjectLayoutInspector;

namespace Disruptor.Benchmarks
{
    public static class Program
    {
        public static void Main()
        {
            //BenchmarkRunner.Run<RingBufferBenchmarks>();

            TypeLayout.PrintLayout<Sequence>();
            TypeLayout.PrintLayout<RingBuffer<object>>();
            TypeLayout.PrintLayout<SingleProducerSequencer>();

            Console.ReadLine();
        }
    }
}
