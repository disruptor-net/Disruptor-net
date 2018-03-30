using System;
using BenchmarkDotNet.Running;

namespace Disruptor.Benchmarks
{
    public static class Program
    {
        public static void Main()
        {
            //BenchmarkRunner.Run<RingBufferBenchmarks>();
            BenchmarkRunner.Run<MultiProducerSequencerBenchmarks>();

            //TypeLayout.PrintLayout<Sequence>();
            //TypeLayout.PrintLayout<RingBuffer<object>>();
            //TypeLayout.PrintLayout<SingleProducerSequencer>();

            //RunInt32ArrayBenchmarks();
            //RunObjectArrayBenchmarks();

            Console.ReadLine();
        }

        private static void RunInt32ArrayBenchmarks()
        {
            var bench = new Int32ArrayBenchmarks();

            bench.Write();
            bench.WriteFixed();
            bench.WritePointer();
            bench.WriteUnsafe();

            Console.WriteLine("YYY");
            Console.ReadLine();
            Console.WriteLine("ZZZ");

            bench.Write();
            bench.WriteFixed();
            bench.WritePointer();
            bench.WriteUnsafe();
        }

        private static void RunObjectArrayBenchmarks()
        {
            var bench = new ObjectArrayBenchmarks();

            bench.ReadOne();
            bench.ReadOneUnsafe();

            Console.WriteLine("YYY");
            Console.ReadLine();
            Console.WriteLine("ZZZ");

            bench.ReadOne();
            bench.ReadOneUnsafe();
        }
    }
}
