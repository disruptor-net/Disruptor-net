using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Disruptor.PerfTests;

public class PerfTestTypeSelector
{
    private readonly ProgramOptions _options;

    public PerfTestTypeSelector(ProgramOptions options)
    {
        _options = options;
    }

    public List<PerfTestType> GetPerfTestTypes()
    {
        if (_options.Target == null)
            return PrintAndSelectTestTypes();

        if (_options.Target.Equals("all", StringComparison.OrdinalIgnoreCase))
            return GetAllTestTypes();

        return GetMatchingTestTypes(_options.Target, PerfTestType.GeAll());
    }

    private List<PerfTestType> PrintAndSelectTestTypes()
    {
        var testTypes = GetIncludedTestTypes();
        var printableTypes = testTypes.OrderBy(x => x.Type.FullName).Select((x, i) => new PrintableType(i, x)).ToList();

        PrintGroup(0, printableTypes);

        Console.WriteLine();
        Console.WriteLine("Enter type index or type name:");

        var target = Console.ReadLine();

        if (target == null)
            return new();

        if (int.TryParse(target, CultureInfo.InvariantCulture, out var index))
        {
            if (index < 0 || index >= printableTypes.Count)
            {
                Console.WriteLine("Invalid type index");
                return new();
            }

            return new() { printableTypes[index].Type };
        }

        return GetMatchingTestTypes(target, testTypes);
    }

    private static void PrintGroup(int level, List<PrintableType> types)
    {
        var padding = new string(' ', level * 4);
        foreach (var group in types.Where(x => x.Namespace.Length > level).GroupBy(x => x.Namespace[level]))
        {
            Console.WriteLine($"{padding}- {group.Key}");
            PrintGroup(level + 1, group.ToList());
        }

        foreach (var type in types.Where(x => x.Namespace.Length == level))
        {
            Console.WriteLine($"{padding}{type.Index}: {type.Name}");
        }
    }

    private List<PerfTestType> GetAllTestTypes()
    {
        var includedTestTypes = GetIncludedTestTypes();

        return string.IsNullOrEmpty(_options.From)
            ? includedTestTypes
            : includedTestTypes.SkipWhile(x => !x.Type.Name.Equals(_options.From)).ToList();
    }

    private List<PerfTestType> GetIncludedTestTypes()
    {
        return PerfTestType.GeAll().Where(IsIncluded).ToList();

        bool IsIncluded(PerfTestType type)
        {
            if (typeof(IExternalTest).IsAssignableFrom(type.Type) && !_options.IncludeExternal)
                return false;

            if (typeof(IThroughputTest).IsAssignableFrom(type.Type) && _options.IncludeThroughput)
                return true;

            if (typeof(ILatencyTest).IsAssignableFrom(type.Type) && _options.IncludeLatency)
                return true;

            return false;
        }
    }

    private static List<PerfTestType> GetMatchingTestTypes(string target, IEnumerable<PerfTestType> testTypes)
    {
        var regex = CreateTargetRegex(target);

        return testTypes.Where(x => regex.IsMatch(x.Type.Name)).ToList();

        static Regex CreateTargetRegex(string target)
        {
            var prefix = target.StartsWith("*") ? "" : "^";
            var suffix = target.EndsWith("*") ? "" : "$";

            return new Regex($"{prefix}{target.Trim('*')}{suffix}");
        }
    }

    private class PrintableType
    {
        public PrintableType(int index, PerfTestType type)
        {
            Index = index;
            Type = type;
            Namespace = type.Namespace != null ? type.Namespace.Split('.') : Array.Empty<string>();
        }

        public int Index { get; }
        public PerfTestType Type { get; }
        public string[] Namespace { get; }

        public string Name => Type.Name;
    }
}
