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

            Console.WriteLine($"PID: {options.ProcessId}");

            using (var dataTarget = DataTarget.AttachToProcess(options.ProcessId, 5000, AttachFlag.NonInvasive))
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
                        ScanEvents(runtime);
                    else
                        ScanTypes(runtime, options.ScanTypeName);

                }
            }

            Console.WriteLine();
            Console.WriteLine("Press enter to exit");
            Console.ReadLine();
        }

        private static void ScanEvents(ClrRuntime runtime)
        {
            Console.WriteLine($"Scanning for ring buffers");

            var ringBufferPadding = 2 * 128 / IntPtr.Size;

            for (var segmentIndex = 0; segmentIndex < runtime.Heap.Segments.Count; segmentIndex++)
            {
                Console.WriteLine($"Walking the heap... ({segmentIndex} / {runtime.Heap.Segments.Count})");

                var foundForSegment = false;
                var segment = runtime.Heap.Segments[segmentIndex];
                for (var address = segment.FirstObject; address != 0; address = segment.NextObject(address))
                {
                    var type = runtime.Heap.GetObjectType(address);
                    if (type == null)
                        continue;

                    if (!type.Name.StartsWith("Disruptor.RingBuffer<") || !type.Name.EndsWith(">"))
                        continue;

                    if (!foundForSegment)
                    {
                        Console.WriteLine();
                        foundForSegment = true;
                    }
                    Console.WriteLine($"Found instance of {type.Name}");

                    var fieldsField = type.GetFieldByName("_fields");
                    var fieldsAddress = fieldsField.GetAddress(address);

                    var entriesField = fieldsField.Type.GetFieldByName("Entries");
                    var entriesAddress = (ulong)entriesField.GetValue(fieldsAddress, true);
                    var entriesLength = entriesField.Type.GetArrayLength(entriesAddress);

                    Console.WriteLine($"Ring buffer size: {entriesLength} ({entriesLength - ringBufferPadding} events + {ringBufferPadding} padding)");

                    var entriesAddresses = new List<ulong>();

                    for (var index = 0; index < entriesLength; index++)
                    {
                        var arrayElementValue = (ulong)entriesField.Type.GetArrayElementValue(entriesAddress, index);
                        if (arrayElementValue != 0)
                            entriesAddresses.Add(arrayElementValue);
                    }

                    PrintOffsets(runtime.Heap, entriesAddresses);

                    Console.WriteLine();
                }
            }

            Console.WriteLine($"Walking the heap... ({runtime.Heap.Segments.Count} / {runtime.Heap.Segments.Count})");
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

            for (var segmentIndex = 0; segmentIndex < runtime.Heap.Segments.Count; segmentIndex++)
            {
                Console.WriteLine($"Walking the heap... ({segmentIndex} / {runtime.Heap.Segments.Count})");

                var segment = runtime.Heap.Segments[segmentIndex];
                for (var address = segment.FirstObject; address != 0; address = segment.NextObject(address))
                {
                    var type = runtime.Heap.GetObjectType(address);
                    if (type == null)
                        continue;

                    if (!typeInfos.TryGetValue(type, out var typeInfo))
                        continue;

                    typeInfo.Addresses.Add(address);
                    typeInfo.Generations.Add(segment.GetGeneration(address));
                }
            }

            Console.WriteLine($"Walking the heap... ({runtime.Heap.Segments.Count} / {runtime.Heap.Segments.Count})");

            foreach (var typeInfo in typeInfos)
            {
                Console.WriteLine();
                Console.WriteLine($"[{typeInfo.Key}]");

                typeInfo.Value.PrintStats(runtime.Heap);
            }
        }

        private static void PrintOffsets(ClrHeap heap, List<ulong> addresses)
        {
            if (addresses.Count == 0)
                return;

            var segments = new HashSet<ulong>();
            var offsetCounts = new Dictionary<long, int>();
            var previous = addresses[0];

            segments.Add(heap.GetSegmentByAddress(previous).Start);

            foreach (var address in addresses.Skip(1))
            {
                var offset = address > previous ? (long)(address - previous) : -(long)(previous - address);
                segments.Add(heap.GetSegmentByAddress(address).Start);

                offsetCounts[offset] = offsetCounts.TryGetValue(offset, out var count) ? count + 1 : 1;
                previous = address;
            }

            Console.WriteLine($"Segments count: {segments.Count}");

            var sortedOffsetCounts = offsetCounts.Select(x => (offset: x.Key, count: x.Value, absOffset: Math.Abs(x.Key))).OrderBy(x => x.absOffset).ToList();

            foreach (var offsetCount in sortedOffsetCounts.Take(5))
            {
                Console.WriteLine($"Offset: {offsetCount.offset} Count: {offsetCount.count}");
            }

            var remainingOffsets = sortedOffsetCounts.Skip(5).ToList();
            if (remainingOffsets.Count != 0)
            {
                var eventsCount = remainingOffsets.Sum(x => x.count);
                var maxOffset = remainingOffsets.Max(x => x.absOffset);
                var averageOffset = (long)remainingOffsets.Average(x => x.absOffset);

                Console.WriteLine($"{remainingOffsets.Count} remaining offsets for {eventsCount} events, max: {maxOffset}, avg: {averageOffset}");
            }
        }

        private class TypeInfo
        {
            public List<ulong> Addresses { get; } = new();
            public HashSet<int> Generations { get; } = new();

            public void PrintStats(ClrHeap heap)
            {
                if (!Addresses.Any())
                {
                    Console.WriteLine("No object found");
                    return;
                }

                Console.WriteLine($"Generations: {string.Join(", ", Generations.OrderBy(_ => _))}");

                Addresses.Sort();

                PrintOffsets(heap, Addresses);
            }
        }

        public class Options
        {
            public int ProcessId { get; set; }
            public string ScanTypeName { get; set; }
            public bool ScanEvents { get; set; }

            public static bool TryParse(string[] args, out Options options)
            {
                options = new Options();

                if (args.Length == 0 || !int.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out var pid))
                    return false;

                options.ProcessId = pid;

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
