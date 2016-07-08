using System;
using Disruptor.Collections;
using Disruptor.PerfTests.Runner;

namespace Disruptor.PerfTests
{
    public abstract class LatencyPerfTest : PerfTest
    {
        protected LatencyPerfTest(int iterations) : base(iterations)
        {
        }

        public abstract void RunPass();
        public abstract Histogram Histogram { get; }

        protected override void RunAsUnitTest()
        {
            RunPass();

            Console.WriteLine("{0} : {1}", GetType().Name, Histogram);
        }

        public override TestRun CreateTestRun(int pass, int availableCores)
        {
            return new LatencyTestRun(this, pass, availableCores);
        }
    }
}