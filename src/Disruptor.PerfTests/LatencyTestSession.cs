using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using HdrHistogram;

namespace Disruptor.PerfTests;

public class LatencyTestSession
{
    private readonly Type _perfTestType;
    private readonly Program.Options _options;
    private readonly string _resultDirectoryPath;
    private readonly int _runCount;

    public LatencyTestSession(Type perfTestType, Program.Options options, string resultDirectoryPath)
    {
        _perfTestType = perfTestType;
        _options = options;
        _resultDirectoryPath = resultDirectoryPath;
        _runCount = options.RunCount ?? 3;
    }

    public void Execute()
    {
        var test = (ILatencyTest)Activator.CreateInstance(_perfTestType);

        try
        {
            CheckProcessorsRequirements(test);

            var results = Run(test);
            Report(test, results);
        }
        finally
        {
            if (test is IDisposable disposable)
                disposable.Dispose();
        }
    }

    public List<LatencyTestSessionResult> Run(ILatencyTest test)
    {
        Console.WriteLine($"Latency Test to run => {_perfTestType.FullName}, Runs => {_runCount}");
        Console.WriteLine("Starting");

        var results = new List<LatencyTestSessionResult>();


        for (var i = 0; i < _runCount; i++)
        {
            var histogram = new LongHistogram(10000000000L, 4);
            var stopwatch = new Stopwatch();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            var beforeGen0Count = GC.CollectionCount(0);
            var beforeGen1Count = GC.CollectionCount(1);
            var beforeGen2Count = GC.CollectionCount(2);

            LatencyTestSessionResult result;
            try
            {
                test.Run(stopwatch, histogram);

                var gen0Count = GC.CollectionCount(0) - beforeGen0Count;
                var gen1Count = GC.CollectionCount(1) - beforeGen1Count;
                var gen2Count = GC.CollectionCount(2) - beforeGen2Count;

                result = new LatencyTestSessionResult(histogram, stopwatch.Elapsed, gen0Count, gen1Count, gen2Count);
            }
            catch (Exception ex)
            {
                result = new LatencyTestSessionResult(ex);
            }

            Console.WriteLine(result);
            results.Add(result);
        }

        return results;
    }

    public void Report(ILatencyTest test, List<LatencyTestSessionResult> results)
    {
        var computerSpecifications = new ComputerSpecifications();

        if (_options.ShouldPrintComputerSpecifications)
        {
            Console.WriteLine();
            Console.Write(computerSpecifications.ToString());
        }

        if (!_options.ShouldGenerateReport)
            return;

        var path = Path.Combine(_resultDirectoryPath, $"{_perfTestType.Name}-{DateTime.Now:yyyy-MM-dd hh-mm-ss}.html");

        File.WriteAllText(path, BuildReport(test, results, computerSpecifications));

        var totalsPath = Path.Combine(_resultDirectoryPath, $"Totals-{DateTime.Now:yyyy-MM-dd}.csv");
        foreach (var result in results)
        {
            File.AppendAllText(totalsPath, FormattableString.Invariant($"{DateTime.Now:HH:mm:ss},{_perfTestType.Name},{result.P(50)},{result.P(90)},{result.P(99)}{Environment.NewLine}"));
        }

        if (_options.ShouldOpenReport)
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

    private string BuildReport(ILatencyTest test, List<LatencyTestSessionResult> results, ComputerSpecifications computerSpecifications)
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
        if (test.RequiredProcessorCount > Environment.ProcessorCount)
            sb.AppendLine("        Warning ! Test requires: " + test.RequiredProcessorCount + " processors but there is only " + Environment.ProcessorCount + " here <br>");

        sb.AppendLine("        <h2>Detailed test results</h2>");
        sb.AppendLine("        <table border=\"1\">");
        sb.AppendLine("            <tr>");
        sb.AppendLine("                <td>Run</td>");
        sb.AppendLine("                <td>Latencies (hdr histogram output)</td>");
        sb.AppendLine("                <td>Duration (ms)</td>");
        sb.AppendLine("                <td># GC (0-1-2)</td>");
        sb.AppendLine("            </tr>");

        for (var i = 0; i < results.Count; i++)
        {
            var result = results[i];
            result.AppendDetailedHtmlReport(i, sb);
        }

        sb.AppendLine("        </table>");

        return sb.ToString();
    }
}
