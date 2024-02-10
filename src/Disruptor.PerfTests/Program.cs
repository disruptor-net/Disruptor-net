using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Disruptor.PerfTests;

public class Program
{
    public static void Main(string[] args)
    {
        if (!ProgramOptions.TryParse(args, out var options))
        {
            ProgramOptions.PrintUsage();
            return;
        }

        if (!ValidateOptions(options))
            return;

        var selector = new PerfTestTypeSelector(options);
        var testTypes = selector.GetPerfTestTypes();

        foreach (var testType in testTypes)
        {
            RunTestForType(testType, options);
        }
    }

    private static bool ValidateOptions(ProgramOptions options)
    {
        if (options.Target != null && !Regex.IsMatch(options.Target, @"\\*\w+(?:.\w+)*\\*"))
        {
            Console.WriteLine($"Invalid target: [{options.Target}]");
            return false;
        }

        if (options.From != null && !Regex.IsMatch(options.From, @"\w+(?:.\w+)*"))
        {
            Console.WriteLine($"Invalid from: [{options.From}]");
            return false;
        }

        return true;
    }

    private static void RunTestForType(Type perfTestType, ProgramOptions options)
    {
        var outputDirectoryPath = Path.Combine(AppContext.BaseDirectory, "results");
        if (options.GenerateReport)
            Directory.CreateDirectory(outputDirectoryPath);

        var isThroughputTest = typeof(IThroughputTest).IsAssignableFrom(perfTestType);
        if (isThroughputTest)
        {
            var session = new ThroughputTestSession(perfTestType, options, outputDirectoryPath);
            session.Execute();
            return;
        }

        var isLatencyTest = typeof(ILatencyTest).IsAssignableFrom(perfTestType);
        if (isLatencyTest)
        {
            var session = new LatencyTestSession(perfTestType, options, outputDirectoryPath);
            session.Execute();
            return;
        }

        throw new NotSupportedException($"Invalid test type: {perfTestType.Name}");
    }
}
