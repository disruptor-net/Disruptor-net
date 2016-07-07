using System.Threading;
using Atomic;

namespace Disruptor.PerfTests.Support
{
    public class FizzBuzzEventHandler : IEventHandler<FizzBuzzEvent>
    {
        private readonly FizzBuzzStep _fizzBuzzStep;
        private readonly long _iterations;
        private readonly ManualResetEvent _mru;
        private Volatile.PaddedLong _fizzBuzzCounter;

        public long FizzBuzzCounter
        {
            get { return _fizzBuzzCounter.ReadUnfenced(); }
        }

        public FizzBuzzEventHandler(FizzBuzzStep fizzBuzzStep, long iterations, ManualResetEvent mru)
        {
            _fizzBuzzStep = fizzBuzzStep;
            _iterations = iterations;
            _mru = mru;
            _fizzBuzzCounter = new Volatile.PaddedLong(0);
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
                        _fizzBuzzCounter.WriteUnfenced(_fizzBuzzCounter.ReadUnfenced() + 1);
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
