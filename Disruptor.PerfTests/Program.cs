using System;
using System.Linq;
using System.Reflection;
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


            Type[] perfTestTypes;
            if (args[0] == "ALL")
            {
                perfTestTypes = Assembly.GetAssembly(typeof(Program))
                                        .GetTypes()
                                        .Where(x => !x.IsAbstract && (typeof(IThroughputTest).IsAssignableFrom(x) || typeof(ILatencyTest).IsAssignableFrom(x)))
                                        .OrderBy(x => x.Name)
                                        .ToArray();
            }
            else
            {
                var type = Type.GetType(args[0]);
                if (type == null)
                {
                    Console.WriteLine($"Could not find the type '{args[0]}'");
                    return;
                }
                perfTestTypes = new[] { type };
            }

            foreach (var perfTestType in perfTestTypes)
            {
                RunTestForType(perfTestType);
            }
        }

        private static void RunTestForType(Type perfTestType)
        {
            var isThroughputTest = typeof(IThroughputTest).IsAssignableFrom(perfTestType);
            var isLatencyTest = typeof(ILatencyTest).IsAssignableFrom(perfTestType);

            var typeName = perfTestType.Name;
            if (!isThroughputTest && !isLatencyTest)
            {
                Console.WriteLine($"*** ERROR *** Unable to determine the runner to use for this type ({typeName})");
                return;
            }

            //Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;

            var computerSpecifications = new ComputerSpecifications();
            Console.WriteLine(computerSpecifications.ToString());

            if (isThroughputTest)
            {
                var session = new ThroughputTestSession(computerSpecifications, perfTestType);
                session.Run();
                session.GenerateAndOpenReport();
            }

            if (isLatencyTest)
            {
                var session = new LatencyTestSession(computerSpecifications, perfTestType);
                session.Run();
                session.GenerateAndOpenReport();
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: Disruptor.PerfTests TestTypeFullName|ALL [ie. Disruptor.PerfTests.Sequenced.OneToOneSequencedBatchThroughputTest]");
        }
    }
}