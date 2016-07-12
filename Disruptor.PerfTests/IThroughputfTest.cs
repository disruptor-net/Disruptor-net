using System.Diagnostics;
using HdrHistogram;

namespace Disruptor.PerfTests
{
    public interface IThroughputfTest
    {
        long Run(Stopwatch stopwatch);

        int RequiredProcessorCount { get; }
    }

    public interface ILatencyTest
    {
        void Run(Stopwatch stopwatch, HistogramBase histogram);

        int RequiredProcessorCount { get; }
    }
}