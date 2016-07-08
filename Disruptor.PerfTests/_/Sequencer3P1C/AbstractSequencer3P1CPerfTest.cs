namespace Disruptor.PerfTests.Sequencer3P1C
{
    public abstract class AbstractSequencer3P1CPerfTest:ThroughputPerfTest
    {
        protected const int NumProducers = 3;
        protected const int Size = 1024 * 32;

        protected AbstractSequencer3P1CPerfTest(int iterations) : base(iterations)
        {
        }

        public override int MinimumCoresRequired
        {
            get { return 4; }
        }
    }
}