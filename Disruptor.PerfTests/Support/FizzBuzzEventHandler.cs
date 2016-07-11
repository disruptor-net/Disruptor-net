using System.Threading;
using Disruptor.Tests.Support;

namespace Disruptor.PerfTests.Support
{
    public class FizzBuzzEventHandler : IEventHandler<FizzBuzzEvent>
    {
        private readonly FizzBuzzStep _fizzBuzzStep;
        private readonly long _iterations;
        private readonly ManualResetEvent _mru;
        private PaddedLong _fizzBuzzCounter;

        public long FizzBuzzCounter => _fizzBuzzCounter.Value;

        public FizzBuzzEventHandler(FizzBuzzStep fizzBuzzStep, long iterations, ManualResetEvent mru)
        {
            _fizzBuzzStep = fizzBuzzStep;
            _iterations = iterations;
            _mru = mru;
        }

        public void OnEvent(FizzBuzzEvent data, long sequence, bool endOfBatch)
        {
            switch (_fizzBuzzStep)
            {
                case FizzBuzzStep.Fizz:
                    data.Fizz = (data.Value%3) == 0;
                    break;
                case FizzBuzzStep.Buzz:
                    data.Buzz = (data.Value % 5) == 0;
                    break;

                case FizzBuzzStep.FizzBuzz:
                    if (data.Fizz && data.Buzz)
                    {
                        _fizzBuzzCounter.Value = _fizzBuzzCounter.Value + 1;
                    }
                    break;
            }
            if(sequence == _iterations - 1)
            {
                _mru.Set();
            }
        }
    }
}
