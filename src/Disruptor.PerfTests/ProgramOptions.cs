using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Disruptor.PerfTests;

public class ProgramOptions
{
    public static int DefaultRunCountForLatencyTest { get; } = 3;
    public static int DefaultRunCountForThroughputTest { get; } = 7;
    public static int[] DefaultCpuSet { get; } = Enumerable.Range(0, Environment.ProcessorCount).ToArray();

    public static IReadOnlyDictionary<string, Func<IWaitStrategy>> ConfigurableWaitStrategies { get; } = new Dictionary<string, Func<IWaitStrategy>>(StringComparer.OrdinalIgnoreCase)

    {
        ["yielding"] = () => new YieldingWaitStrategy(),
        ["blocking"] = () => new BlockingWaitStrategy(),
        ["busy-spin"] = () => new BusySpinWaitStrategy(),
    };

    private int[] _cpuSet = DefaultCpuSet;

    public int? RunCount { get; private set; }
    public string Target { get; private set; }
    public bool PrintComputerSpecifications { get; private set; } = true;
    public bool GenerateReport { get; private set; } = true;
    public bool OpenReport { get; private set; }
    public string From { get; private set; }
    public bool IncludeExternal { get; private set; }
    public bool IncludeLatency { get; private set; } = true;
    public bool IncludeThroughput { get; private set; } = true;
    public string IpcPublisherPath { get; private set; }

    public int[] CpuSet
    {
        get => _cpuSet;
        private set
        {
            _cpuSet = value ?? DefaultCpuSet;
            HasCustomCpuSet = value != null;
        }
    }

    public bool HasCustomCpuSet { get; private set; }

    public Func<IWaitStrategy> WaitStrategySource { get; set; }

    public int RunCountForLatencyTest => RunCount ?? DefaultRunCountForLatencyTest;
    public int RunCountForThroughputTest => RunCount ?? DefaultRunCountForThroughputTest;

    public int? GetCustomCpu(int index)
        => HasCustomCpuSet ? CpuSet[index] : null;

    public IWaitStrategy GetWaitStrategy()
        => GetWaitStrategy<YieldingWaitStrategy>();

    public IWaitStrategy GetWaitStrategy<TDefault>()
        where TDefault : IWaitStrategy, new()
        => WaitStrategySource != null ? WaitStrategySource.Invoke() : new TDefault();

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

            if (arg.Equals("--cpus", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 == args.Length || Regex.Match(args[index + 1], @"(?<cpu0>\d+)(?:,(?<cpun>\d+))*") is not { Success: true} match)
                    return false;

                options.CpuSet = [int.Parse(match.Groups["cpu0"].Value), ..match.Groups["cpun"].Captures.Select(x => int.Parse(x.Value))];
                index++;
                continue;
            }

            if (arg.Equals("--wait-strategy", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 == args.Length || !ConfigurableWaitStrategies.TryGetValue(args[index + 1], out var waitStrategy))
                    return false;

                options.WaitStrategySource = waitStrategy;
                index++;
                continue;
            }

            if (arg.Equals("--ipc-publisher-path", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 == args.Length)
                    return false;

                options.IpcPublisherPath = args[index + 1];
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
        Console.WriteLine($"  --runs <count>                      Number of runs per test. Default is {DefaultRunCountForThroughputTest} for throughput tests and {DefaultRunCountForLatencyTest} for latency tests.");
        Console.WriteLine($"  --from <name>                       The first test type name to run when running all tests.");
        Console.WriteLine($"  --include-external <true|false>     Includes external tests. Default is {options.IncludeExternal}.");
        Console.WriteLine($"  --include-latency <true|false>      Includes latency tests. Default is {options.IncludeLatency}.");
        Console.WriteLine($"  --include-throughput <true|false>   Includes throughput tests. Default is {options.IncludeThroughput}.");
        Console.WriteLine($"  --cpus <cpu-set>                    The comma-separated list of CPUs to use for CPU affinity (not supported in all tests).");
        Console.WriteLine($"  --wait-strategy <type>              The disruptor wait strategy. Supported values: {string.Join(", ", ConfigurableWaitStrategies.Keys)}. (not supported in all tests).");
        Console.WriteLine($"  --ipc-publisher-path <path>         Path of the IPC publisher executable.");
        Console.WriteLine();
    }

    public bool Validate()
    {
        if (Target != null && !Regex.IsMatch(Target, @"\\*\w+(?:.\w+)*\\*"))
        {
            Console.WriteLine($"Invalid target: [{Target}]");
            return false;
        }

        if (From != null && !Regex.IsMatch(From, @"\w+(?:.\w+)*"))
        {
            Console.WriteLine($"Invalid from: [{From}]");
            return false;
        }

        if (HasCustomCpuSet && CpuSet.Except(DefaultCpuSet).Any())
        {
            Console.WriteLine($"Invalid cpus: [{string.Join(", ", CpuSet)}], available CPU range: [0-{Environment.ProcessorCount - 1}]");
            return false;
        }

        if (IpcPublisherPath != null && !File.Exists(IpcPublisherPath))
        {
            Console.WriteLine($"Invalid IPC publisher path: [{IpcPublisherPath}]");
            return false;
        }

        return true;
    }


}
