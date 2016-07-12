using System;
using Disruptor.PerfTests.Sequenced;

namespace Disruptor.PerfTests
{
    public class Program
    {
        static void Main(string[] args)
        {
            if (args == null || args.Length != 1)
            {
                PrintUsage();
                Console.ReadKey();
                return;
            }

            var computerSpecifications = new ComputerSpecifications();
            Console.WriteLine(computerSpecifications.ToString());

            var perfTestType = Type.GetType(args[0]);
            if (perfTestType == null)
            {
                Console.WriteLine($"Could not find the type '{args[0]}'");
                return;
            }

            var session = new PerformanceTestSession(computerSpecifications, perfTestType);

            //Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;

            session.Run();

            session.GenerateAndOpenReport();
            Console.ReadKey();
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: Disruptor.PerfTests TestTypeFullName [ie. Disruptor.PerfTests.Sequenced.OneToOneSequencedBatchThroughputTest]");
        }
    }
}