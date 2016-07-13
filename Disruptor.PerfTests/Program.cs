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

            var perfTestType = Type.GetType(args[0]);
            if (perfTestType == null)
            {
                Console.WriteLine($"Could not find the type '{args[0]}'");
                return;
            }

            var isThroughputTest = args[0].Contains("Throughput");
            var isLatencyTest = args[0].Contains("Latency");

            if (!isThroughputTest && !isLatencyTest)
            {
                Console.WriteLine($"*** ERROR *** Unable to determine the runner to use for this type ({args[0]})");
                return;
            }

            //Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;

            var computerSpecifications = new ComputerSpecifications();
            Console.WriteLine(computerSpecifications.ToString());

            if (isThroughputTest)
            {
                var session = new ThroughputTestSession(computerSpecifications, Type.GetType(args[0]));
                session.Run();
                session.GenerateAndOpenReport();
            }

            if (isLatencyTest)
            {
                var session = new LatencyTestSession(computerSpecifications, Type.GetType(args[0]));
                session.Run();
                session.GenerateAndOpenReport();
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: Disruptor.PerfTests TestTypeFullName [ie. Disruptor.PerfTests.Sequenced.OneToOneSequencedBatchThroughputTest]");
        }
    }
}