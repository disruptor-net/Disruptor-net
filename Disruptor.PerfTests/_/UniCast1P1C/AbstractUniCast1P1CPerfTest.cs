namespace Disruptor.PerfTests.UniCast1P1C
{
    public abstract class AbstractUniCast1P1CPerfTest : ThroughputPerfTest
    {
        private long _expectedResult;
        protected const int BufferSize = 1024 * 8;

        protected AbstractUniCast1P1CPerfTest(int iterations) : base(iterations)
        {
        }

        protected long ExpectedResult
        {
            get
            {
                if (_expectedResult == 0)
                {
                     for (var i = 0L; i < Iterations; i++)
                    {
                        _expectedResult += i;
                    }
                }
                return _expectedResult;
            }
        }

        public override int MinimumCoresRequired
        {
            get { return 2; }
        }
    }
}