using System;
using System.Collections.Generic;
using System.Text;

namespace Disruptor.PerfTests.Runner
{
    public class Implementation
    {
        private readonly string _scenarioName;
        private readonly string _implementationName;
        private readonly IList<TestRun> _testRuns = new List<TestRun>();
        
        public Implementation(string scenarioName, string implementationName, int runs, int numberOfCores)
        {
            _scenarioName = scenarioName;
            _implementationName = implementationName;
            var ns = "Disruptor.PerfTests." + scenarioName;
            var className = scenarioName + implementationName + "PerfTest";
            var classFullName = ns + "." + className;

            var perfTestType = Type.GetType(classFullName);
            
            for (int i = 0; i < runs; i++)
            {
                var perfTest = (PerfTest)Activator.CreateInstance(perfTestType);
                _testRuns.Add(perfTest.CreateTestRun(i, numberOfCores));
            }
        }

        public void Run()
        {
            foreach (var testRun in _testRuns)
            {
                testRun.Run();
            }
        }

        public void AppendDetailedHtmlReport(StringBuilder sb)
        {
            foreach (var testRun in _testRuns)
            {
                sb.AppendLine("            <tr>");
                sb.AppendLine("                <td>" + _scenarioName + "</td>");
                sb.AppendLine("                <td>" + _implementationName + "</td>");
                testRun.AppendResultHtml(sb);
                sb.AppendLine("            </tr>");
            }
        }
    }
}