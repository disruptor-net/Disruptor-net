using System;
using Disruptor.PerfTests.Sequenced;

namespace Disruptor.PerfTests
{
    public class Program
    {
        public static void Main(string[] args)
        {
            OneToOneSequencedBatchThroughputTest.Run();

            Console.ReadLine();
        }
    }
}