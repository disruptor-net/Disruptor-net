using System;
using System.Linq;
using System.Reflection;
using Disruptor.PerfTests.Queue;

namespace Disruptor.PerfTests
{
    public class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0 || args.Length > 2)
            {
                PrintUsage();
                Console.ReadKey();
                return;
            }
            
            Type[] perfTestTypes;
            if (string.Equals(args[0], "ALL", StringComparison.OrdinalIgnoreCase))
            {
                var startAt = args.Length == 2 ? args[1] : null;
                perfTestTypes = Assembly.GetAssembly(typeof(Program))
                                        .GetTypes()
                                        .Where(x => !x.IsAbstract && (typeof(IThroughputTest).IsAssignableFrom(x) || typeof(ILatencyTest).IsAssignableFrom(x)) && !typeof(IQueueTest).IsAssignableFrom(x))
                                        .OrderBy(x => x.Name)
                                        .SkipWhile(type => startAt != null && type.Name != startAt)
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
                RunTestForType(perfTestType, perfTestTypes.Length == 1);
            }
        }

        private static void RunTestForType(Type perfTestType, bool shouldOpen)
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

            if (isThroughputTest)
            {
                var session = new ThroughputTestSession(perfTestType);
                session.Run();
                session.GenerateAndOpenReport(shouldOpen);
            }

            if (isLatencyTest)
            {
                var session = new LatencyTestSession(perfTestType);
                session.Run();
                session.GenerateAndOpenReport(shouldOpen);
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: Disruptor.PerfTests TestTypeFullName|ALL [ie. Disruptor.PerfTests.Sequenced.OneToOneSequencedBatchThroughputTest]");
        }
    }
}