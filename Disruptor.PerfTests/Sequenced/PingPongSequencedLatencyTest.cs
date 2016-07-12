using System.Diagnostics;
using HdrHistogram;

namespace Disruptor.PerfTests.Sequenced
{
    public class PingPongSequencedLatencyTest : ILatencyTest
    {
        public void Run(Stopwatch stopwatch, HistogramBase histogram)
        {
            stopwatch.Start();
            for (var i = 0; i < 1000 * 1000; i++)
            {
                histogram.RecordValue(i);
            }
            stopwatch.Stop();
        }

        public int RequiredProcessorCount { get; } = 2;
    }
}