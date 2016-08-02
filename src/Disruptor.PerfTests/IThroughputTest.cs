using System.Diagnostics;

namespace Disruptor.PerfTests
{
    public interface IThroughputTest
    {
        long Run(Stopwatch stopwatch);

        int RequiredProcessorCount { get; }
    }
}