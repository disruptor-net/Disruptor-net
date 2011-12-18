using System;
using System.Diagnostics;
using Disruptor.PerfTests.Runner;

namespace Disruptor.PerfTests
{
    public static class Program
    {
        static void Main(string[] args)
        {
            ScenarioType scenarioType;
            ImplementationType implementationType;
            int runs;

            if (args == null
                || args.Length != 3
                || !Enum.TryParse(args[0], out scenarioType)
                || !Enum.TryParse(args[1], out implementationType)
                || !int.TryParse(args[2], out runs)
                )
            {
                PrintUsage();
                return;
            }
            
            Console.WriteLine(new ComputerSpecifications().ToString());

            var session = new PerformanceTestSession(scenarioType, implementationType, runs);

            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;

            session.Run();

            session.GenerateAndOpenReport();
            Console.ReadKey();
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: Disruptor.PerfTests Scenario Implementation Runs");
            Console.WriteLine();
            PrintEnum(typeof (ScenarioType));
            Console.WriteLine();
            PrintEnum(typeof(ImplementationType));
            Console.WriteLine();
            Console.WriteLine("Runs: number of test run to do for each scenario and implementation");
            Console.WriteLine();
            Console.WriteLine("Example: Disruptor.PerfTests 1 1");
            Console.WriteLine("will run UniCast1P1C performance test with the disruptor only.");
        }

        private static void PrintEnum(Type enumType)
        {
            var names = Enum.GetNames(enumType);
            var values = Enum.GetValues(enumType);

            Console.WriteLine(enumType.Name + " options:");

            for (var i = 0; i < names.Length; i++)
            {
                var name = names[i];
                var value = (int)values.GetValue(i);
                Console.WriteLine(" - {0} ({1})", value, name);
            }
        }
    }
}

