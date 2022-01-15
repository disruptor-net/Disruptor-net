using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using HdrHistogram;

namespace Disruptor.PerfTests
{
    public class LatencyTestSession
    {
        private static readonly double _stopwatchTickNanoSecondsFrequency = 1000000000.0 / Stopwatch.Frequency;

        private readonly List<LatencyTestSessionResult> _results = new(10);
        private readonly Type _perfTestType;
        private ILatencyTest _test;
        private int _runCount;

        public LatencyTestSession(Type perfTestType)
        {
            _perfTestType = perfTestType;
        }

        public void Run(Program.Options options)
        {
            _runCount = options.RunCount ?? 3;

            Console.WriteLine($"Latency Test to run => {_perfTestType.FullName}, Runs => {_runCount}");

            _test = (ILatencyTest)Activator.CreateInstance(_perfTestType);
            CheckProcessorsRequirements(_test);

            Console.WriteLine("Starting");
            
            for (var i = 0; i < _runCount; i++)
            {
                var histogram = new LongHistogram(10000000000L, 4);
                var stopwatch = new Stopwatch();

                GC.Collect();
                GC.WaitForPendingFinalizers();

                var beforeGen0Count = GC.CollectionCount(0);
                var beforeGen1Count = GC.CollectionCount(1);
                var beforeGen2Count = GC.CollectionCount(2);

                Exception exception = null;
                LatencyTestSessionResult result = null;
                try
                {
                    _test.Run(stopwatch, histogram);
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                if (exception != null)
                {
                    result = new LatencyTestSessionResult(exception);
                }
                else
                {
                    var gen0Count = GC.CollectionCount(0) - beforeGen0Count;
                    var gen1Count = GC.CollectionCount(1) - beforeGen1Count;
                    var gen2Count = GC.CollectionCount(2) - beforeGen2Count;

                    result = new LatencyTestSessionResult(histogram, stopwatch.Elapsed, gen0Count, gen1Count, gen2Count);
                }

                Console.WriteLine(result);
                _results.Add(result);
            }
        }

        public void Report(Program.Options options)
        {
            var computerSpecifications = new ComputerSpecifications();

            if (options.ShouldPrintComputerSpecifications)
            {
                Console.WriteLine();
                Console.Write(computerSpecifications.ToString());
            }

            if (!options.ShouldGenerateReport)
                return;

            var path = Path.Combine(Environment.CurrentDirectory, _perfTestType.Name + "-" + DateTime.Now.ToString("yyyy-MM-dd hh-mm-ss") + ".html");

            File.WriteAllText(path, BuildReport(computerSpecifications));

            var totalsPath = Path.Combine(Environment.CurrentDirectory, $"Totals-{DateTime.Now:yyyy-MM-dd}.csv");
            File.AppendAllText(totalsPath, $"{DateTime.Now:HH:mm:ss},{_perfTestType.Name},{_results.Max(x => x.Histogram.GetValueAtPercentile(99))}\n");

            if (options.ShouldOpenReport)
                Process.Start(path);
        }

        private static void CheckProcessorsRequirements(ILatencyTest test)
        {
            var availableProcessors = Environment.ProcessorCount;
            if (test.RequiredProcessorCount <= availableProcessors)
                return;

            Console.WriteLine("*** Warning ***: your system has insufficient processors to execute the test efficiently. ");
            Console.WriteLine($"Processors required = {test.RequiredProcessorCount}, available = {availableProcessors}");
        }

        private string BuildReport(ComputerSpecifications computerSpecifications)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE HTML PUBLIC \"-//W3C//DTD HTML 4.0 Transitional//EN\">")
                .AppendLine("<html>")
                .AppendLine("	<head>")
                .AppendLine("		<title>Disruptor-net - Test Report</title>")
                .AppendLine("	</head>")
                .AppendLine("	<body>")
                .AppendLine("        Local time: " + DateTime.Now + "<br>")
                .AppendLine("        UTC time: " + DateTime.UtcNow);

            sb.AppendLine("        <h2>Host configuration</h2>");
            computerSpecifications.AppendHtml(sb);

            if (computerSpecifications.PhysicalCoreCount < 4)
            {
                sb.AppendFormat("        <b><font color='red'>Your computer has {0} physical core(s) but most of the tests require at least 4 cores</font></b><br>", computerSpecifications.PhysicalCoreCount);
            }
            if (!Stopwatch.IsHighResolution)
            {
                sb.AppendFormat("        <b><font color='red'>Your computer does not support synchronized TSC, measured latencies might be wrong on multicore CPU architectures.</font></b><br>", computerSpecifications.PhysicalCoreCount);
            }
            if (computerSpecifications.IsHyperThreaded)
            {
                sb.AppendLine("        <b><font color='red'>Hyperthreading can degrade performance, you should turn it off.</font></b><br>");
            }

            sb.AppendLine("        <h2>Test configuration</h2>")
              .AppendLine("        Test: " + _perfTestType.FullName + "<br>")
              .AppendLine("        Runs: " + _runCount + "<br>");
            if (_test.RequiredProcessorCount > Environment.ProcessorCount)
                sb.AppendLine("        Warning ! Test requires: " + _test.RequiredProcessorCount + " processors but there is only " + Environment.ProcessorCount + " here <br>");

            sb.AppendLine("        <h2>Detailed test results</h2>");
            sb.AppendLine("        <table border=\"1\">");
            sb.AppendLine("            <tr>");
            sb.AppendLine("                <td>Run</td>");
            sb.AppendLine("                <td>Latencies (hdr histogram output)</td>");
            sb.AppendLine("                <td>Duration (ms)</td>");
            sb.AppendLine("                <td># GC (0-1-2)</td>");
            sb.AppendLine("            </tr>");

            for (var i = 0; i < _results.Count; i++)
            {
                var result = _results[i];
                result.AppendDetailedHtmlReport(i, sb);
            }

            sb.AppendLine("        </table>");

            return sb.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ConvertStopwatchTicksToNano(long durationInTicks)
        {
            return (long)(durationInTicks * _stopwatchTickNanoSecondsFrequency);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ConvertNanoToStopwatchTicks(long pauseDurationInNanos)
        {
            return (long)(pauseDurationInNanos / _stopwatchTickNanoSecondsFrequency);
        }
    }
}
