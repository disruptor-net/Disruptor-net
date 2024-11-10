using System.Diagnostics;
using HdrHistogram;

namespace Disruptor.PerfTests;

public interface ILatencyTest
{
    int RequiredProcessorCount { get; }

    void Run(LatencySessionContext sessionContext);
}
