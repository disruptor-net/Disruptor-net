using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Disruptor.PerfTests;

public class LatencyTestSession : IDisposable
{
    private readonly ILatencyTest _test;
    private readonly ProgramOptions _options;
    private readonly string _resultDirectoryPath;

    public LatencyTestSession(ILatencyTest test, ProgramOptions options, string resultDirectoryPath)
    {
        _test = test;
        _options = options;
        _resultDirectoryPath = resultDirectoryPath;
    }

    public void Execute()
    {
        var results = Run();
        Report(results);
    }

    public List<LatencyTestSessionResult> Run()
    {
        Console.Write($"Latency Test to run => {_test.GetType().FullName}, Runs => {_options.RunCountForLatencyTest}");
        if (_options.HasCustomCpuSet)
            Console.Write($", Cpus: [{string.Join(", ", _options.CpuSet)}]");

        Console.WriteLine();
        Console.WriteLine("Starting");

        var results = new List<LatencyTestSessionResult>();
        var context = new LatencySessionContext();

        for (var i = 0; i < _options.RunCountForLatencyTest; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();

            context.Reset();

            var beforeGen0Count = GC.CollectionCount(0);
            var beforeGen1Count = GC.CollectionCount(1);
            var beforeGen2Count = GC.CollectionCount(2);

            LatencyTestSessionResult result;
            try
            {
                _test.Run(context);

                var gen0Count = GC.CollectionCount(0) - beforeGen0Count;
                var gen1Count = GC.CollectionCount(1) - beforeGen1Count;
                var gen2Count = GC.CollectionCount(2) - beforeGen2Count;

                result = new LatencyTestSessionResult(context.Histogram, context.ElapsedTime, gen0Count, gen1Count, gen2Count);
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

    public void Report(List<LatencyTestSessionResult> results)
    {
        var computerSpecifications = new ComputerSpecifications();

        if (_options.PrintComputerSpecifications)
        {
            Console.WriteLine();
            Console.Write(computerSpecifications.ToString());
        }

        if (!_options.GenerateReport)
            return;

        var path = Path.Combine(_resultDirectoryPath, $"{_test.GetType().Name}-{DateTime.Now:yyyy-MM-dd hh-mm-ss}.html");

        File.WriteAllText(path, BuildReport(results, computerSpecifications));

        var totalsPath = Path.Combine(_resultDirectoryPath, $"Totals-{DateTime.Now:yyyy-MM-dd}.csv");
        foreach (var result in results)
        {
            File.AppendAllText(totalsPath, FormattableString.Invariant($"{DateTime.Now:HH:mm:ss},{_test.GetType().Name},{result.P(50)},{result.P(90)},{result.P(99)}{Environment.NewLine}"));
        }

        if (_options.OpenReport)
            Process.Start(path);
    }

    private string BuildReport(List<LatencyTestSessionResult> results, ComputerSpecifications computerSpecifications)
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
          .AppendLine("        Test: " + _test.GetType().FullName + "<br>")
          .AppendLine("        Runs: " + _options.RunCountForLatencyTest + "<br>")
          .AppendLine("        <h2>Detailed test results</h2>")
          .AppendLine("        <table border=\"1\">")
          .AppendLine("            <tr>")
          .AppendLine("                <td>Run</td>")
          .AppendLine("                <td>Latencies (hdr histogram output)</td>")
          .AppendLine("                <td>Duration (ms)</td>")
          .AppendLine("                <td># GC (0-1-2)</td>")
          .AppendLine("            </tr>");

        for (var i = 0; i < results.Count; i++)
        {
            var result = results[i];
            result.AppendDetailedHtmlReport(i, sb);
        }

        sb.AppendLine("        </table>");

        return sb.ToString();
    }

    public void Dispose()
    {
        if (_test is IDisposable disposable)
            disposable.Dispose();
    }
}
