using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Disruptor.PerfTests.External;

namespace Disruptor.PerfTests;

public class Program
{
    public static void Main(string[] args)
    {
        if (!Options.TryParse(args, out var options))
        {
            Options.PrintUsage();
            return;
        }

        if (!TryLoadPerfTestTypes(options, out var perfTestTypes))
        {
            Console.WriteLine($"Invalid target: [{options.Target}]");
            return;
        }

        foreach (var perfTestType in perfTestTypes)
        {
            RunTestForType(perfTestType, options);
        }
    }

    private static bool TryLoadPerfTestTypes(Options options, out Type[] perfTestTypes)
    {
        if ("all".Equals(options.Target, StringComparison.OrdinalIgnoreCase))
        {
            perfTestTypes = typeof(Program).Assembly.GetTypes().Where(x => IsValidTestType(x) && !typeof(IExternalTest).IsAssignableFrom(x)).ToArray();
            if (!string.IsNullOrEmpty(options.From))
                perfTestTypes = perfTestTypes.SkipWhile(x => !x.Name.EndsWith(options.From)).ToArray();

            return true;
        }

        var type = Resolve(options.Target);
        if (type != null && IsValidTestType(type))
        {
            perfTestTypes = new[] { type };
            return true;
        }

        perfTestTypes = null;
        return false;

        bool IsValidTestType(Type t) => !t.IsAbstract && (typeof(IThroughputTest).IsAssignableFrom(t) || typeof(ILatencyTest).IsAssignableFrom(t));
        Type Resolve(string typeName) => Type.GetType(typeName) ?? typeof(Program).Assembly.ExportedTypes.FirstOrDefault(x => x.Name == typeName);
    }

    private static void RunTestForType(Type perfTestType, Options options)
    {
        var isThroughputTest = typeof(IThroughputTest).IsAssignableFrom(perfTestType);
        if (isThroughputTest)
        {
            var session = new ThroughputTestSession(perfTestType, options);
            session.Execute();
            return;
        }

        var isLatencyTest = typeof(ILatencyTest).IsAssignableFrom(perfTestType);
        if (isLatencyTest)
        {
            var session = new LatencyTestSession(perfTestType, options);
            session.Execute();
            return;
        }

        throw new NotSupportedException($"Invalid test type: {perfTestType.Name}");
    }

    public class Options
    {
        public int? RunCount { get; set; }
        public string Target { get; set; }
        public bool ShouldPrintComputerSpecifications { get; set; }
        public bool ShouldGenerateReport { get; set; }
        public bool ShouldOpenReport { get; set; }
        public string From { get; set; }
        public bool PerfCountersEnabled { get; set; }

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
                switch (arg)
                {
                    case { } s when Regex.Match(s, "--report=(true|false)", RegexOptions.IgnoreCase) is var m && m.Success:
                        options.ShouldGenerateReport = bool.Parse(m.Groups[1].Value);
                        break;

                    case { } s when Regex.Match(s, "--openreport=(true|false)", RegexOptions.IgnoreCase) is var m && m.Success:
                        options.ShouldOpenReport = bool.Parse(m.Groups[1].Value);
                        break;

                    case { } s when Regex.Match(s, "--printspec=(true|false)", RegexOptions.IgnoreCase) is var m && m.Success:
                        options.ShouldPrintComputerSpecifications = bool.Parse(m.Groups[1].Value);
                        break;

                    case { } s when Regex.Match(s, "--runs=(\\d+)", RegexOptions.IgnoreCase) is var m && m.Success:
                        options.RunCount = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                        break;

                    case { } s when Regex.Match(s, "--from=(\\w+)", RegexOptions.IgnoreCase) is var m && m.Success:
                        options.From = m.Groups[1].Value;
                        break;

                    case { } s when Regex.Match(s, "--perfcounters=(true|false)", RegexOptions.IgnoreCase) is var m && m.Success:
                        options.PerfCountersEnabled = bool.Parse(m.Groups[1].Value);
                        break;

                    default:
                        return false;
                }
            }

            return true;
        }

        public static void PrintUsage()
        {
            Console.WriteLine($"Usage:");
            Console.WriteLine($"  {AppDomain.CurrentDomain.FriendlyName} <TARGET> [options]");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  <TARGET>                   The test type name or \"all\" for all tests");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --report=<true|false>      Generate an HTML report file at the end of the test. Default is true.");
            Console.WriteLine("  --openreport=<true|false>  Opens the HTML report file at the end of the test. Default is false.");
            Console.WriteLine("  --printspec=<true|false>   Prints computer specifications. Default is true.");
            Console.WriteLine("  --runs=<COUNT>             Number of runs per test");
            Console.WriteLine("  --from=<NAME>              The first test type name to run when running all tests");
            Console.WriteLine();
        }
    }
}
