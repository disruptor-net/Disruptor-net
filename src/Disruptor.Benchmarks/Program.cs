using System;
using BenchmarkDotNet.Running;

namespace Disruptor.Benchmarks
{
    public static class Program
    {
        public static void Main()
        {
            //BenchmarkRunner.Run<RingBufferBenchmarks>();
            //BenchmarkRunner.Run<MultiProducerSequencerBenchmarks>();
            //BenchmarkRunner.Run<ObjectArrayBenchmarks>();
            //BenchmarkRunner.Run<Int32ArrayBenchmarks>();
            BenchmarkRunner.Run<ValueArrayBenchmarks>();

            //TypeLayout.PrintLayout<Sequence>();
            //TypeLayout.PrintLayout<RingBuffer<object>>();
            //TypeLayout.PrintLayout<SingleProducerSequencer>();

            //RunMultiProducerSequencerBenchmarks();
            //RunInt32ArrayBenchmarks();
            //RunObjectArrayBenchmarks();
            //RunRingBufferBenchmarks();

            //Console.WriteLine(ObjectArrayBenchmarks.OffsetToArrayData);
            //Console.WriteLine(Int32ArrayBenchmarks.OffsetToArrayData);

            Console.ReadLine();
        }

        private static void RunRingBufferBenchmarks()
        {
            var bench = new RingBufferBenchmarks();

            //bench.Indexer();
            //bench.IndexerArray();
            //bench.IndexerUnsafe();
            bench.IndexerUnsafeSum();
            bench.IndexerUnsafeSumForLoop();

            Console.WriteLine("X");
            Console.ReadLine();

            //bench.Indexer();
            //bench.IndexerArray();
            //bench.IndexerUnsafe();
            bench.IndexerUnsafeSum();
            bench.IndexerUnsafeSumForLoop();
        }

        private static void RunMultiProducerSequencerBenchmarks()
        {
            var bench = new MultiProducerSequencerBenchmarks();

            bench.IsAvailable();
            bench.IsAvailablePointer();
            bench.Publish();
            bench.PublishPointer();

            Console.WriteLine("YYY");
            Console.ReadLine();
            Console.WriteLine("ZZZ");

            bench.IsAvailable();
            bench.IsAvailablePointer();
            bench.Publish();
            bench.PublishPointer();
        }

        private static void RunInt32ArrayBenchmarks()
        {
            var bench = new Int32ArrayBenchmarks();

            //bench.Write();
            //bench.WriteFixed();
            //bench.WritePointer();
            //bench.WriteUnsafe();
            bench.Read();
            bench.ReadFixed();

            Console.WriteLine("YYY");
            Console.ReadLine();

            //bench.Write();
            //bench.WriteFixed();
            //bench.WritePointer();
            //bench.WriteUnsafe();
            bench.Read();
            bench.ReadFixed();
        }

        private static void RunObjectArrayBenchmarks()
        {
            var bench = new ObjectArrayBenchmarks();

            bench.ReadImplPublic(371);
            bench.ReadUnsafeImplPublic(371);

            Console.WriteLine("YYY");
            Console.ReadLine();
            Console.WriteLine("ZZZ");

            bench.ReadImplPublic(371);
            bench.ReadUnsafeImplPublic(371);
        }
    }
}
