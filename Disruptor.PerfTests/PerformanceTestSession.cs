using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Disruptor.PerfTests
{
    public class PerformanceTestSession
    {
        public const int Runs = 7;

        private readonly ComputerSpecifications _computerSpecifications;
        private readonly List<ThroughputTestSessionResult> _results = new List<ThroughputTestSessionResult>(Runs);
        private readonly Type _perfTestType;
        private IPerfTest _test;

        public PerformanceTestSession(ComputerSpecifications computerSpecifications, Type perfTestType)
        {
            _computerSpecifications = computerSpecifications;
            _perfTestType = perfTestType;
            Console.WriteLine($"Test to run => {_perfTestType.FullName}, Runs => {Runs}");
        }

        public void Run()
        {
            _test = (IPerfTest)Activator.CreateInstance(_perfTestType);
            CheckProcessorsRequirements(_test);

            Console.WriteLine("Starting perf tests");
            var stopwatch = new Stopwatch();
            for (var i = 0; i < Runs; i++)
            {
                stopwatch.Reset();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                
                var beforeGen0Count = GC.CollectionCount(0);
                var beforeGen1Count = GC.CollectionCount(1);
                var beforeGen2Count = GC.CollectionCount(2);

                long totalOperationsInRun = 0;
                Exception exception = null;
                ThroughputTestSessionResult result = null;
                try
                {
                    totalOperationsInRun = _test.Run(stopwatch);
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

                    result = new ThroughputTestSessionResult(totalOperationsInRun, stopwatch.Elapsed, gen0Count, gen1Count, gen2Count);
                }

                
                Console.WriteLine(result);
                _results.Add(result);
            }
        }

        private void CheckProcessorsRequirements(IPerfTest test)
        {
            var availableProcessors = Environment.ProcessorCount;
            if (test.RequiredProcessorCount <= availableProcessors)
                return;

            Console.WriteLine("*** Warning ***: your system has insufficient processors to execute the test efficiently. ");
            Console.WriteLine($"Processors required = {test.RequiredProcessorCount}, available = {availableProcessors}");
        }

        public string BuildReport()
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
            _computerSpecifications.AppendHtml(sb);

            if(_computerSpecifications.NumberOfCores < 4)
            {
                sb.AppendFormat("        <b><font color='red'>Your computer has {0} physical core(s) but most of the tests require at least 4 cores</font></b><br>", _computerSpecifications.NumberOfCores);
            }
            if (_computerSpecifications.IsHyperThreaded)
            {
                sb.AppendLine("        <b><font color='red'>Hyperthreading can degrade performance, you should turn it off.</font></b><br>");
            }

            sb.AppendLine("        <h2>Test configuration</h2>")
              .AppendLine("        Test: " + _perfTestType.FullName + "<br>")
              .AppendLine("        Runs: " + Runs + "<br>");
            if (_test.RequiredProcessorCount > Environment.ProcessorCount)
                sb.AppendLine("        Warning ! Test requires: " + _test.RequiredProcessorCount + " processors but there is only " + Environment.ProcessorCount + " here <br>");
            
            sb.AppendLine("        <h2>Detailed test results</h2>");
            sb.AppendLine("        <table border=\"1\">");
            sb.AppendLine("            <tr>");
            sb.AppendLine("                <td>Run</td>");
            sb.AppendLine("                <td>Operations per second</td>");
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

        public void GenerateAndOpenReport()
        {
            var path = Path.Combine(Environment.CurrentDirectory,
                                    "TestReport-" + DateTime.UtcNow.ToString("yyyy-MM-dd hh-mm-ss") + ".html");

            File.WriteAllText(path, BuildReport());

            Process.Start(path);
        }
    }
}