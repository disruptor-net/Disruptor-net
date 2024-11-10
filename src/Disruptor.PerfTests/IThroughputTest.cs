using System.Diagnostics;

namespace Disruptor.PerfTests;

public interface IThroughputTest
{
    int RequiredProcessorCount { get; }

    long Run(ThroughputSessionContext sessionContext);
}
