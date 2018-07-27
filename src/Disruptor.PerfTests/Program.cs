using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Disruptor.PerfTests.Queue;

namespace Disruptor.PerfTests
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (!Options.TryParse(args, out var options))
            {
                Options.PrintUsage();
                Console.ReadKey();
                return;
            }

            if (!TryLoadPerfTestTypes(options.Target, out var perfTestTypes))
            {
                Console.WriteLine($"Invalid target: [{options.Target}]");
                Console.ReadKey();
                return;
            }

            foreach (var perfTestType in perfTestTypes)
            {
                RunTestForType(perfTestType, options);
            }
        }

        private static bool TryLoadPerfTestTypes(string target, out Type[] perfTestTypes)
        {
            if ("all".Equals(target, StringComparison.OrdinalIgnoreCase))
            {
                perfTestTypes = typeof(Program).Assembly.GetTypes().Where(x => IsValidTestType(x) && !typeof(IQueueTest).IsAssignableFrom(x)).ToArray();
                return true;
            }

            var type = Type.GetType(target);
            if (type != null && IsValidTestType(type))
            {
                perfTestTypes = new[] { type };
                return true;
            }

            perfTestTypes = null;
            return false;

            bool IsValidTestType(Type x) => !x.IsAbstract && (typeof(IThroughputTest).IsAssignableFrom(x) || typeof(ILatencyTest).IsAssignableFrom(x));
        }

        private static void RunTestForType(Type perfTestType, Options options)
        {
            var isThroughputTest = typeof(IThroughputTest).IsAssignableFrom(perfTestType);
            var isLatencyTest = typeof(ILatencyTest).IsAssignableFrom(perfTestType);

            //Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;

            if (isThroughputTest)
            {
                var session = new ThroughputTestSession(perfTestType);
                session.Run(options);
                session.Report(options);
            }

            if (isLatencyTest)
            {
                var session = new LatencyTestSession(perfTestType);
                session.Run(options);
                session.Report(options);
            }
        }

        public class Options
        {
            public int? RunCount { get; set; }
            public string Target { get; set; }
            public bool ShouldPrintComputerSpecifications { get; set; }
            public bool ShouldGenerateReport { get; set; }
            public bool ShouldOpenReport { get; set; }

            public static bool TryParse(string[] args, out Options options)
            {
                options = new Options
                {
                    ShouldPrintComputerSpecifications = true,
                    ShouldGenerateReport = true,
                    ShouldOpenReport = false,
                };

                if (args.Length == 0 || string.IsNullOrEmpty(args[0]))
                    return false;

                options.Target = args[0];

                foreach (var arg in args.Skip(1))
                {
                    switch (arg.ToLowerInvariant())
                    {
                        case "--report=false":
                            options.ShouldGenerateReport = false;
                            break;

                        case "--openreport=true":
                            options.ShouldOpenReport = true;
                            break;

                        case "--printspec=false":
                            options.ShouldPrintComputerSpecifications = false;
                            break;

                        case string s when Regex.Match(s, "--runs=(\\d+)") is var m && m.Success:
                            options.RunCount = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                            break;

                        default:
                            return false;
                    }
                }

                return true;
            }

            public static void PrintUsage()
            {
                Console.WriteLine($"Usage: {AppDomain.CurrentDomain.FriendlyName} target [--report=false] [--openreport=false] [--printspec=false] [--runs=count]");
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("     target         Test type full name or \"all\" for all tests");
                Console.WriteLine("   --runs count     Number of runs");
                Console.WriteLine();
            }
        }
    }
}
