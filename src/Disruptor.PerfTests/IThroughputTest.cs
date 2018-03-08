using System.Diagnostics;

namespace Disruptor.PerfTests
{
    public interface IThroughputTest
    {
        long Run(ThroughputSessionContext sessionContext);

        int RequiredProcessorCount { get; }
    }
}