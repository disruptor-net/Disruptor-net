using System.Threading;
using Disruptor.Tests.Support;

namespace Disruptor.PerfTests.Support
{
    public class FunctionEventHandler : IEventHandler<FunctionEvent>
    {
        private readonly FunctionStep _functionStep;
        private PaddedLong _stepThreeCounter;
        private readonly long _iterations;
        private readonly ManualResetEvent _mru;

        public long StepThreeCounter => _stepThreeCounter.Value;

        public FunctionEventHandler(FunctionStep functionStep, long iterations, ManualResetEvent mru)
        {
            _functionStep = functionStep;
            _iterations = iterations;
            _mru = mru;
        }

        public void OnEvent(FunctionEvent data, long sequence, bool endOfBatch)
        {
            switch (_functionStep)
            {
                case FunctionStep.One:
                    data.StepOneResult = data.OperandOne + data.OperandTwo;
                    break;
                case FunctionStep.Two:
                    data.StepTwoResult = data.StepOneResult + 3L;
                    break;

                case FunctionStep.Three:
                    if ((data.StepTwoResult & 4L) == 4L)
                    {
                        _stepThreeCounter.Value = _stepThreeCounter.Value + 1;
                    }
                    break;
            }

            if(sequence == _iterations-1)
            {
                _mru.Set();
            }
        }
    }
}