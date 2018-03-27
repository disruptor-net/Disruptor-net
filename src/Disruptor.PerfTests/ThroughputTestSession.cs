using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Disruptor.PerfTests
{
    public class ThroughputTestSession
    {
        private readonly List<ThroughputTestSessionResult> _results = new List<ThroughputTestSessionResult>(10);
        private readonly Type _perfTestType;
        private IThroughputTest _test;
        private int _runCount;

        public ThroughputTestSession(Type perfTestType)
        {
            _perfTestType = perfTestType;
        }

        public void Run(Program.Options options)
        {
            _runCount = options.RunCount ?? 7;

            Console.WriteLine($"Throughput Test to run => {_perfTestType.FullName}, Runs => {_runCount}");

            _test = (IThroughputTest)Activator.CreateInstance(_perfTestType);
            CheckProcessorsRequirements(_test);

            Console.WriteLine("Starting");
            var context = new ThroughputSessionContext();

            for (var i = 0; i < _runCount; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();

                context.Reset();

                var beforeGen0Count = GC.CollectionCount(0);
                var beforeGen1Count = GC.CollectionCount(1);
                var beforeGen2Count = GC.CollectionCount(2);

                long totalOperationsInRun = 0;
                Exception exception = null;
                ThroughputTestSessionResult result;
                try
                {
                    totalOperationsInRun = _test.Run(context);
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                if (exception != null)
                {
                    result = new ThroughputTestSessionResult(exception);
                }
                else
                {
                    var gen0Count = GC.CollectionCount(0) - beforeGen0Count;
                    var gen1Count = GC.CollectionCount(1) - beforeGen1Count;
                    var gen2Count = GC.CollectionCount(2) - beforeGen2Count;

                    result = new ThroughputTestSessionResult(totalOperationsInRun, context.Stopwatch.Elapsed, gen0Count, gen1Count, gen2Count, context);
                }

                Console.WriteLine(result);
                _results.Add(result);
            }
        }

        public void Report(Program.Options options)
        {
            var computerSpecifications = new ComputerSpecifications();

            if (options.ShouldPrintComputerSpecifications)
                Console.WriteLine(computerSpecifications.ToString());

            if (!options.ShouldGenerateReport)
                return;

            var path = Path.Combine(Environment.CurrentDirectory, _perfTestType.Name + "-" + DateTime.UtcNow.ToString("yyyy-MM-dd hh-mm-ss") + ".html");
            File.WriteAllText(path, BuildReport(computerSpecifications));

            var totalsPath = Path.Combine(Environment.CurrentDirectory, $"Totals-{DateTime.Now:yyyy-MM-dd}.csv");
            File.AppendAllText(totalsPath, $"{DateTime.Now:HH:mm:ss},{_perfTestType.Name},{_results.Average(x => x.TotalOperationsInRun / x.Duration.TotalSeconds)}\n");

            if (options.ShouldOpenReport)
                Process.Start(path);
        }

        private void CheckProcessorsRequirements(IThroughputTest test)
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
            if (computerSpecifications.NumberOfCores < 4)
            {
                sb.AppendFormat("        <b><font color='red'>Your computer has {0} physical core(s) but most of the tests require at least 4 cores</font></b><br>", computerSpecifications.NumberOfCores);
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
            sb.AppendLine("                <td>Operations per second</td>");
            sb.AppendLine("                <td>Duration (ms)</td>");
            sb.AppendLine("                <td># GC (0-1-2)</td>");
            sb.AppendLine("                <td>Batch %</td>");
            sb.AppendLine("                <td>Average Batch Size<td>");
            sb.AppendLine("            </tr>");

            for (var i = 0; i < _results.Count; i++)
            {
                var result = _results[i];
                result.AppendDetailedHtmlReport(i, sb);
            }

            sb.AppendLine("        </table>");

            return sb.ToString();
        }
    }
}
