using Disruptor.PerfTests.Support;

namespace Disruptor.PerfTests.MultiCast1P3C
{
    public abstract class AbstractMultiCast1P3CPerfTest:ThroughputPerfTest
    {
        protected const int NumEventProcessors = 3;
        protected const int Size = 1024 * 32;
        private long[] _results;

        protected AbstractMultiCast1P3CPerfTest(int iterations) : base(iterations)
        {
        }

        protected long[] ExpectedResults
        {
            get
            {
                if (_results == null)
                {
                    _results = new long[NumEventProcessors];
                    for (long i = 0; i < Iterations; i++)
                    {
                        _results[0] = Operation.Addition.Op(_results[0], i);
                        _results[1] = Operation.Substraction.Op(_results[1], i);
                        _results[2] = Operation.And.Op(_results[2], i);
                    }
                }
                return _results;
            }
        }

        public override int MinimumCoresRequired
        {
            get { return 4; }
        }
    }
}