using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Disruptor.PerfTests;

public static class PerfTestTypePrinter
{
    [RequiresUnreferencedCode("")]
    public static void Main(string[] args)
    {
        var testTypes = typeof(Program).Assembly.GetTypes().Where(IsValidPerfTestType).ToList();

        foreach (var testType in testTypes.OrderBy(x => x.FullName))
        {
            Console.WriteLine($"new(typeof({testType.FullName.Replace("Disruptor.PerfTests.", "")})),");
        }

        static bool IsValidPerfTestType(Type t) => !t.IsAbstract && (typeof(IThroughputTest).IsAssignableFrom(t) || typeof(ILatencyTest).IsAssignableFrom(t));
    }
}
