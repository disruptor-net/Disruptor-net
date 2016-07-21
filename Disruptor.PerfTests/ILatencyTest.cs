using System.Diagnostics;
using HdrHistogram;

namespace Disruptor.PerfTests
{
    public interface ILatencyTest
    {
        void Run(Stopwatch stopwatch, HistogramBase histogram);

        int RequiredProcessorCount { get; }
    }
}