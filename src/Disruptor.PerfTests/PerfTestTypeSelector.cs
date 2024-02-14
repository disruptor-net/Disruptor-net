using System;
using System.Collections.Generic;
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

    public List<Type> GetPerfTestTypes()
    {
        if (_options.Target == null)
            return PrintAndSelectTestTypes();

        if (_options.Target.Equals("all", StringComparison.OrdinalIgnoreCase))
            return GetAllTestTypes();

        return GetMatchingTestTypes(_options.Target, LoadValidTestTypes());
    }

    private List<Type> PrintAndSelectTestTypes()
    {
        var testTypes = GetIncludedTestTypes();
        var printableTypes = testTypes.OrderBy(x => x.FullName).Select((x, i) => new PrintableType(i, x)).ToList();

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

    private List<Type> GetAllTestTypes()
    {
        var includedTestTypes = GetIncludedTestTypes();

        return string.IsNullOrEmpty(_options.From)
            ? includedTestTypes
            : includedTestTypes.SkipWhile(x => !x.Name.Equals(_options.From)).ToList();
    }

    private List<Type> GetIncludedTestTypes()
    {
        return LoadValidTestTypes().Where(IsIncluded).ToList();

        bool IsIncluded(Type type)
        {
            if (typeof(IExternalTest).IsAssignableFrom(type) && !_options.IncludeExternal)
                return false;

            if (typeof(IThroughputTest).IsAssignableFrom(type) && _options.IncludeThroughput)
                return true;

            if (typeof(ILatencyTest).IsAssignableFrom(type) && _options.IncludeLatency)
                return true;

            return false;
        }
    }

    private static List<Type> GetMatchingTestTypes(string target, List<Type> testTypes)
    {
        var regex = CreateTargetRegex(target);

        return testTypes.Where(x => regex.IsMatch(x.Name)).ToList();

        static Regex CreateTargetRegex(string target)
        {
            var prefix = target.StartsWith("*") ? "" : "^";
            var suffix = target.EndsWith("*") ? "" : "$";

            return new Regex($"{prefix}{target.Trim('*')}{suffix}");
        }
    }

    private static List<Type> LoadValidTestTypes()
    {
        return typeof(Program).Assembly.GetTypes().Where(IsValidPerfTestType).ToList();

        static bool IsValidPerfTestType(Type t) => !t.IsAbstract && (typeof(IThroughputTest).IsAssignableFrom(t) || typeof(ILatencyTest).IsAssignableFrom(t));
    }

    private class PrintableType
    {
        public PrintableType(int index, Type type)
        {
            Index = index;
            Type = type;
            Namespace = type.Namespace != null ? type.Namespace.Split('.') : Array.Empty<string>();
            Name = type.Name;
        }

        public int Index { get; }
        public Type Type { get; }
        public string[] Namespace { get; }
        public string Name { get; }
    }
}
