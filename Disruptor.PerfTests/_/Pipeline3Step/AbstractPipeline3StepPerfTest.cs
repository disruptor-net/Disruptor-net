namespace Disruptor.PerfTests.Pipeline3Step
{
    public abstract class AbstractPipeline3StepPerfTest:ThroughputPerfTest
    {
        protected const int Size = 1024 * 32;
        private long _expectedResult;
        protected const long OperandTwoInitialValue = 777L;

        protected AbstractPipeline3StepPerfTest(int iterations) : base(iterations)
        {
        }

        protected long ExpectedResult
        {
            get
            {
                if (_expectedResult == 0)
                {
                    var operandTwo = OperandTwoInitialValue;

                    for (long i = 0; i < Iterations; i++)
                    {
                        var stepOneResult = i + operandTwo--;
                        var stepTwoResult = stepOneResult + 3;

                        if ((stepTwoResult & 4L) == 4L)
                        {
                            ++_expectedResult;
                        }
                    }
                }
                return _expectedResult;
            }
        }

        public override int MinimumCoresRequired
        {
            get { return 4; }
        }
    }
}