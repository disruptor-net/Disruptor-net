using System;
using System.Collections.Concurrent;

namespace Disruptor.PerfTests.Support
{
    public class FizzBuzzQueueEventProcessor
    {
        private readonly FizzBuzzStep _fizzBuzzStep;
        private readonly BlockingCollection<long> _fizzInputQueue;
        private readonly BlockingCollection<long> _buzzInputQueue;
        private readonly BlockingCollection<bool> _fizzOutputQueue;
        private readonly BlockingCollection<bool> _buzzOutputQueue;
        private readonly long _iterations;
        private volatile bool _done;
        private volatile bool _running;
        private long _fizzBuzzCounter;

        public FizzBuzzQueueEventProcessor(FizzBuzzStep fizzBuzzStep,
                                 BlockingCollection<long> fizzInputQueue,
                                 BlockingCollection<long> buzzInputQueue,
                                 BlockingCollection<bool> fizzOutputQueue,
                                 BlockingCollection<bool> buzzOutputQueue,
                                 long iterations)
        {
            _fizzBuzzStep = fizzBuzzStep;

            _fizzInputQueue = fizzInputQueue;
            _buzzInputQueue = buzzInputQueue;
            _fizzOutputQueue = fizzOutputQueue;
            _buzzOutputQueue = buzzOutputQueue;
            _iterations = iterations;
            _done = false;
        }

        public bool Done
        {
            get { return _done; }
        }

        public long FizzBuzzCounter
        {
            get { return _fizzBuzzCounter; }
        }

        public void Reset()
        {
            _done = false;
            _fizzBuzzCounter = 0;
        }

        public void Halt()
        {
            _running = false;
        }

        public void Run()
        {
            _running = true;

            for (var i = 0; i < _iterations; i++)
            {
                try
                {
                    switch (_fizzBuzzStep)
                    {
                        case FizzBuzzStep.Fizz:
                            {
                                var value = _fizzInputQueue.Take();
                                _fizzOutputQueue.TryAdd((value % 3) == 0);
                                break;
                            }

                        case FizzBuzzStep.Buzz:
                            {
                                var value = _buzzInputQueue.Take();
                                _buzzOutputQueue.TryAdd((value % 5) == 0);
                                break;
                            }

                        case FizzBuzzStep.FizzBuzz:
                            {
                                var fizz = _fizzOutputQueue.Take();
                                var buzz = _buzzOutputQueue.Take();
                                if (fizz && buzz)
                                {
                                    ++_fizzBuzzCounter;
                                }
                                break;
                            }
                    }

                   
                }
                catch (Exception)
                {
                    break;
                }
            }
            _done = true;
        }
    }
}