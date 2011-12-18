using System.Threading;
using Disruptor.MemoryLayout;

namespace Disruptor.PerfTests.Support
{
    public class FizzBuzzEventHandler : IEventHandler<FizzBuzzEvent>
    {
        private readonly FizzBuzzStep _fizzBuzzStep;
        private readonly long _iterations;
        private readonly ManualResetEvent _mru;
        private PaddedLong _fizzBuzzCounter;

        public long FizzBuzzCounter
        {
            get { return _fizzBuzzCounter.Value; }
        }

        public FizzBuzzEventHandler(FizzBuzzStep fizzBuzzStep, long iterations, ManualResetEvent mru)
        {
            _fizzBuzzStep = fizzBuzzStep;
            _iterations = iterations;
            _mru = mru;
            _fizzBuzzCounter = new PaddedLong(0);
        }

        public void OnNext(FizzBuzzEvent data, long sequence, bool endOfBatch)
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
                        ++_fizzBuzzCounter.Value;
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
