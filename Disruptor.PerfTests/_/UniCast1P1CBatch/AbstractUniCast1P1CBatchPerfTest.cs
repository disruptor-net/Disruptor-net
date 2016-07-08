using Disruptor.PerfTests.UniCast1P1C;

namespace Disruptor.PerfTests.UniCast1P1CBatch
{
    public abstract class AbstractUniCast1P1CBatchPerfTest : AbstractUniCast1P1CPerfTest
    {
        protected AbstractUniCast1P1CBatchPerfTest(int iterations) : base(iterations)
        {
        }
    }
}