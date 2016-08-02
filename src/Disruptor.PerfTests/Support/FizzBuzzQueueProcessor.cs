using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Disruptor.PerfTests.Support
{
    public class FizzBuzzQueueProcessor
    {
        private readonly FizzBuzzStep _fizzBuzzStep;
        private readonly BlockingCollection<long> _fizzInputQueue;
        private readonly BlockingCollection<long> _buzzInputQueue;
        private readonly BlockingCollection<bool> _fizzOutputQueue;
        private readonly BlockingCollection<bool> _buzzOutputQueue;
        private readonly long _count;

        private volatile bool _running;
        private long _fizzBuzzCounter;
        private long _sequence;
        private ManualResetEvent _signal;

        public FizzBuzzQueueProcessor(FizzBuzzStep fizzBuzzStep,
                                 BlockingCollection<long> fizzInputQueue,
                                 BlockingCollection<long> buzzInputQueue,
                                 BlockingCollection<bool> fizzOutputQueue,
                                 BlockingCollection<bool> buzzOutputQueue,
                                 long count)
        {
            _fizzBuzzStep = fizzBuzzStep;

            _fizzInputQueue = fizzInputQueue;
            _buzzInputQueue = buzzInputQueue;
            _fizzOutputQueue = fizzOutputQueue;
            _buzzOutputQueue = buzzOutputQueue;
            _count = count;
        }

        public void Reset(ManualResetEvent signal)
        {
            _fizzBuzzCounter = 0;
            _sequence = 0;
            _signal = signal;
        }

        public void Halt()
        {
            _running = false;
        }

        public void Run()
        {
            _running = true;

            while (_running)
            {
                switch (_fizzBuzzStep)
                {
                    case FizzBuzzStep.Fizz:
                    {
                        long value;
                        while (!_fizzInputQueue.TryTake(out value))
                        {
                            if (!_running)
                                break;
                            Thread.Yield();
                        }
                        while (!_fizzOutputQueue.TryAdd(0 == (value % 3)))
                        {
                            if (!_running)
                                break;
                            Thread.Yield();
                        }
                        break;
                    }

                    case FizzBuzzStep.Buzz:
                    {

                        long value;
                        while (!_buzzInputQueue.TryTake(out value))
                        {
                            if (!_running)
                                break;
                            Thread.Yield();
                        }
                        while (!_buzzOutputQueue.TryAdd(0 == (value % 5)))
                        {
                            if (!_running)
                                break;
                            Thread.Yield();
                        }
                        break;
                    }

                    case FizzBuzzStep.FizzBuzz:
                    {
                        bool fizz;
                        bool buzz;
                        while (!_fizzOutputQueue.TryTake(out fizz))
                        {
                            if (!_running)
                                break;
                            Thread.Yield();
                        }
                        while (!_buzzOutputQueue.TryTake(out buzz))
                        {
                            if (!_running)
                                break;
                            Thread.Yield();
                        }


                        if (fizz && buzz)
                        {
                            ++_fizzBuzzCounter;
                        }
                        break;
                    }
                }

                if (_sequence++ == _count)
                {
                    _signal?.Set();
                }
            }
        }
    }
}