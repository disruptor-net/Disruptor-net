using System;
using System.Collections.Generic;
using System.Text;
using Enumerable = System.Linq.Enumerable;

namespace Disruptor.PerfTests.Runner
{
    public class Scenario
    {
        private readonly IList<Implementation> _implementations = new List<Implementation>();

        public Scenario(string scenarioName, ImplementationType implementationType, int runs, int numberOfCores)
        {
            if (implementationType == ImplementationType.All)
            {
                foreach (var implementationName in Enumerable.Where(Enum.GetNames(typeof(ImplementationType)), s => s != "All"))
                {
                    _implementations.Add(new Implementation(scenarioName, implementationName, runs, numberOfCores));
                }
            }
            else
            {
                string implementationName = implementationType.ToString();
                _implementations.Add(new Implementation(scenarioName, implementationName, runs, numberOfCores));
            }
        }

        public void Run()
        {
            foreach (var implementation in _implementations)
            {
                implementation.Run();
            }
        }

        public void AppendDetailedHtmlReport(StringBuilder sb)
        {
            foreach (var implementation in _implementations)
            {
                implementation.AppendDetailedHtmlReport(sb);
            }
        }
    }
}