using System.Diagnostics;

namespace Disruptor.PerfTests
{
    public interface IPerfTest
    {
        long Run(Stopwatch stopwatch);

        int RequiredProcessorCount { get; }
    }
}