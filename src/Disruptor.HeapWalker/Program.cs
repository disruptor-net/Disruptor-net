using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Diagnostics.Runtime;

namespace Disruptor.HeapWalker
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

            Console.WriteLine($"PID: {options.PID}");

            using (var dataTarget = DataTarget.AttachToProcess(options.PID, 5000, AttachFlag.NonInvasive))
            {
                foreach (var runtimeInfo in dataTarget.ClrVersions)
                {
                    Console.WriteLine($"Runtime: {runtimeInfo.Flavor} {runtimeInfo.Version}");

                    var runtime = runtimeInfo.CreateRuntime();
                    if (!runtime.Heap.CanWalkHeap)
                    {
                        Console.WriteLine("Cannot walk the heap!");
                        continue;
                    }

                    if (options.ScanEvents)
                        ScanEvents();
                    else
                        ScanTypes(runtime, options.ScanTypeName);

                }
            }

            Console.WriteLine();
            Console.WriteLine("Press enter to exit");
            Console.ReadLine();
        }

        private static void ScanEvents()
        {
            Console.WriteLine("Not supported");
        }

        private static void ScanTypes(ClrRuntime runtime, string scanTypeName)
        {
            Console.WriteLine($"Scanning for type {scanTypeName}");

            var typeInfos = runtime.Heap.EnumerateTypes().Where(x => x.Name.EndsWith(scanTypeName)).ToDictionary(_ => _, _ => new TypeInfo());

            if (typeInfos.Count == 0)
            {
                Console.WriteLine("No type found");
                return;
            }

            Console.WriteLine($"Found types: {string.Join(", ", typeInfos.Keys.Select(x => x.Name))}");
            Console.WriteLine();

            for (var segmentIndex = 0; ; segmentIndex++)
            {
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.WriteLine($"Walking the heap... ({segmentIndex} / {runtime.Heap.Segments.Count})");

                if (segmentIndex >= runtime.Heap.Segments.Count)
                    break;

                var segment = runtime.Heap.Segments[segmentIndex];
                for (var address = segment.FirstObject; address != 0; address = segment.NextObject(address))
                {
                    var type = runtime.Heap.GetObjectType(address);
                    if (type == null)
                        continue;

                    if (!typeInfos.TryGetValue(type, out var typeInfo))
                        continue;

                    typeInfo.Addresses.Add(address);
                    typeInfo.Segments.Add(segment.Start);
                    typeInfo.Generations.Add(segment.GetGeneration(address));
                }
            }

            foreach (var typeInfo in typeInfos)
            {
                Console.WriteLine();
                Console.WriteLine($"[{typeInfo.Key}]");

                typeInfo.Value.PrintStats();
            }
        }

        private class TypeInfo
        {
            public List<ulong> Addresses { get; } = new List<ulong>();
            public HashSet<ulong> Segments { get; } = new HashSet<ulong>();
            public HashSet<int> Generations { get; } = new HashSet<int>();

            public void PrintStats()
            {
                if (!Addresses.Any())
                {
                    Console.WriteLine("No object found");
                    return;
                }

                Console.WriteLine($"SegmentCount: {Segments.Count}");
                Console.WriteLine($"Generations: {string.Join(", ", Generations.OrderBy(_ => _))}");

                Addresses.Sort();

                var offsetCounts = new Dictionary<ulong, int>(); 
                var previous = Addresses[0];
                foreach (var address in Addresses.Skip(1))
                {
                    var offset = address - previous;
                    previous = address;

                    offsetCounts[offset] = offsetCounts.TryGetValue(offset, out var count) ? count + 1 : 1;
                }

                foreach (var offsetCount in offsetCounts.OrderBy(x => x.Key))
                {
                    Console.WriteLine($"Offset: {offsetCount.Key} Count: {offsetCount.Value}");
                }
            }
        }
      
        public class Options
        {
            public int PID { get; set; }
            public string ScanTypeName { get; set; }
            public bool ScanEvents { get; set; }

            public static bool TryParse(string[] args, out Options options)
            {
                options = new Options();

                if (args.Length == 0 || !int.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out var pid))
                    return false;

                options.PID = pid;

                foreach (var arg in args.Skip(1))
                {
                    switch (arg)
                    {
                        case string s when Regex.Match(s, "--scan-type=([a-zA-Z0-9._]+)") is var m && m.Success:
                            options.ScanTypeName = m.Groups[1].Value;
                            break;

                        case "--scan-events":
                            options.ScanEvents = true;
                            break;

                        default:
                            return false;
                    }
                }

                return !string.IsNullOrEmpty(options.ScanTypeName) || options.ScanEvents;
            }

            public static void PrintUsage()
            {
                Console.WriteLine($"Usage: {AppDomain.CurrentDomain.FriendlyName} pid [--scan-type=name] [--scan-events]");
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("     pid                   Target process ID");
                Console.WriteLine("   --scan-type=name        Name or fullname of the type to scan");
                Console.WriteLine("   --scan-events           Scan all Disruptor events");
                Console.WriteLine();
            }
        }
    }
}
