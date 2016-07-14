using System.Diagnostics;

namespace Disruptor.PerfTests.Queue
{
    public class OneToThreeDiamondQueueThroughputTest : IThroughputTest, IQueueTest
    {
        public long Run(Stopwatch stopwatch)
        {
            throw new System.NotImplementedException();
        }

        public int RequiredProcessorCount { get; }
    }
}