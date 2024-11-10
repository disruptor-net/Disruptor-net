using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Environments;

namespace Disruptor.PerfTests;

public class ThroughputTestSession : IDisposable
{
    private readonly IThroughputTest _test;
    private readonly ProgramOptions _options;
    private readonly string _resultDirectoryPath;

    public ThroughputTestSession(IThroughputTest test, ProgramOptions options, string resultDirectoryPath)
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

    private List<ThroughputTestSessionResult> Run()
    {
        Console.Write($"Throughput Test to run => {_test.GetType().FullName}, Runs => {_options.RunCountForThroughputTest}");
        if (_options.HasCustomCpuSet)
            Console.Write($", Cpus: [{string.Join(", ", _options.CpuSet)}]");

        Console.WriteLine();
        Console.WriteLine("Starting");

        var results = new List<ThroughputTestSessionResult>();
        var context = new ThroughputSessionContext();

        for (var i = 0; i < _options.RunCountForThroughputTest; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();

            context.Reset();

            var beforeGen0Count = GC.CollectionCount(0);
            var beforeGen1Count = GC.CollectionCount(1);
            var beforeGen2Count = GC.CollectionCount(2);

            ThroughputTestSessionResult result;
            try
            {
                var totalOperationsInRun = _test.Run(context);

                var gen0Count = GC.CollectionCount(0) - beforeGen0Count;
                var gen1Count = GC.CollectionCount(1) - beforeGen1Count;
                var gen2Count = GC.CollectionCount(2) - beforeGen2Count;

                result = new ThroughputTestSessionResult(totalOperationsInRun, context.ElapsedTime, gen0Count, gen1Count, gen2Count, context);
            }
            catch (Exception ex)
            {
                result = new ThroughputTestSessionResult(ex);
            }

            Console.WriteLine(result);
            results.Add(result);
        }

        return results;
    }

    private void Report(List<ThroughputTestSessionResult> results)
    {
        var computerSpecifications = new ComputerSpecifications();

        if (_options.PrintComputerSpecifications)
        {
            Console.WriteLine();
            Console.Write(computerSpecifications.ToString());
        }

        if (!_options.GenerateReport)
            return;

        var path = Path.Combine(_resultDirectoryPath, $"{_test.GetType().Name}-{DateTime.UtcNow:yyyy-MM-dd hh-mm-ss}.html");
        File.WriteAllText(path, BuildReport(results, computerSpecifications));

        var totalsPath = Path.Combine(_resultDirectoryPath, $"Totals-{DateTime.Now:yyyy-MM-dd}.csv");
        var average = results.Average(x => x.TotalOperationsInRun / x.Duration.TotalSeconds);
        File.AppendAllText(totalsPath, FormattableString.Invariant($"{DateTime.Now:HH:mm:ss},{_test.GetType().Name},{average}{Environment.NewLine}"));

        if (_options.OpenReport)
            Process.Start(path);
    }

    private string BuildReport(List<ThroughputTestSessionResult> results, ComputerSpecifications computerSpecifications)
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

        if (computerSpecifications.IsHyperThreaded)
        {
            sb.AppendLine("        <b><font color='red'>Hyperthreading can degrade performance, you should turn it off.</font></b><br>");
        }

        sb.AppendLine("        <h2>Test configuration</h2>")
          .AppendLine("        Test: " + _test.GetType().FullName + "<br>")
          .AppendLine("        Runs: " + _options.RunCountForThroughputTest + "<br>")
          .AppendLine("        <h2>Detailed test results</h2>")
          .AppendLine("        <table border=\"1\">")
          .AppendLine("            <tr>")
          .AppendLine("                <td>Run</td>")
          .AppendLine("                <td>Operations per second</td>")
          .AppendLine("                <td>Duration (ms)</td>")
          .AppendLine("                <td># GC (0-1-2)</td>")
          .AppendLine("                <td>Batch %</td>")
          .AppendLine("                <td>Average Batch Size<td>")
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
