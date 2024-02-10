using System;
using System.Globalization;

namespace Disruptor.PerfTests;

public class ProgramOptions
{
    public int? RunCount { get; set; }
    public string Target { get; set; }
    public bool PrintComputerSpecifications { get; set; } = true;
    public bool GenerateReport { get; set; } = true;
    public bool OpenReport { get; set; }
    public string From { get; set; }
    public bool IncludeExternal { get; set; }
    public bool IncludeLatency { get; set; } = true;
    public bool IncludeThroughput { get; set; } = true;
    public int RunCountForLatencyTest => RunCount ?? 3;
    public int RunCountForThroughputTest => RunCount ?? 7;

    public static bool TryParse(string[] args, out ProgramOptions options)
    {
        options = new ProgramOptions();

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];

            if (arg.Equals("--target", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 == args.Length)
                    return false;

                options.Target = args[index + 1];
                index++;
                continue;
            }

            if (arg.Equals("--report", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 == args.Length || !bool.TryParse(args[index + 1], out var value))
                    return false;

                options.GenerateReport = value;
                index++;
                continue;
            }

            if (arg.Equals("--open-report", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 == args.Length || !bool.TryParse(args[index + 1], out var value))
                    return false;

                options.OpenReport = value;
                index++;
                continue;
            }

            if (arg.Equals("--print-spec", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 == args.Length || !bool.TryParse(args[index + 1], out var value))
                    return false;

                options.PrintComputerSpecifications = value;
                index++;
                continue;
            }

            if (arg.Equals("--runs", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 == args.Length || !int.TryParse(args[index + 1], CultureInfo.InvariantCulture, out var value) || value <= 0)
                    return false;

                options.RunCount = value;
                index++;
                continue;
            }

            if (arg.Equals("--from", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 == args.Length)
                    return false;

                options.From = args[index + 1];
                index++;
                continue;
            }

            if (arg.Equals("--include-external", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 == args.Length || !bool.TryParse(args[index + 1], out var value))
                    return false;

                options.IncludeExternal = value;
                index++;
                continue;
            }

            if (arg.Equals("--include-latency", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 == args.Length || !bool.TryParse(args[index + 1], out var value))
                    return false;

                options.IncludeLatency = value;
                index++;
                continue;
            }

            if (arg.Equals("--include-throughput", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 == args.Length || !bool.TryParse(args[index + 1], out var value))
                    return false;

                options.IncludeThroughput = value;
                index++;
                continue;
            }

            return false;
        }

        return true;
    }

    public static void PrintUsage()
    {
        var options = new ProgramOptions();

        Console.WriteLine($"Usage:");
        Console.WriteLine($"  {AppDomain.CurrentDomain.FriendlyName} [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine($"  --target <target>                   The test type name or \"all\" for all tests.");
        Console.WriteLine($"  --report <true|false>               Generates an HTML report file at the end of the test. Default is {options.GenerateReport}.");
        Console.WriteLine($"  --open-report <true|false>          Opens the HTML report file at the end of the test. Default is {options.OpenReport}.");
        Console.WriteLine($"  --print-spec <true|false>           Prints computer specifications. Default is {options.PrintComputerSpecifications}.");
        Console.WriteLine($"  --runs <count>                      Number of runs per test. Default is {options.RunCountForThroughputTest} for throughput tests and {options.RunCountForLatencyTest} for latency tests.");
        Console.WriteLine($"  --from <name>                       The first test type name to run when running all tests.");
        Console.WriteLine($"  --include-external <true|false>     Includes external tests. Default is {options.IncludeExternal}.");
        Console.WriteLine($"  --include-latency <true|false>      Includes latency tests. Default is {options.IncludeLatency}.");
        Console.WriteLine($"  --include-throughput <true|false>   Includes throughput tests. Default is {options.IncludeThroughput}.");
        Console.WriteLine();
    }
}
