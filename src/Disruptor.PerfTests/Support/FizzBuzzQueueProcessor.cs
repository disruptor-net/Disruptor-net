using System.Collections.Concurrent;
using System.Threading;

namespace Disruptor.PerfTests.Support
{
    public class FizzBuzzQueueProcessor
    {
        private readonly FizzBuzzStep _fizzBuzzStep;
        private readonly ConcurrentQueue<long> _fizzInputQueue;
        private readonly ConcurrentQueue<long> _buzzInputQueue;
        private readonly ConcurrentQueue<bool> _fizzOutputQueue;
        private readonly ConcurrentQueue<bool> _buzzOutputQueue;
        private readonly long _count;

        private volatile bool _running;
        private long _fizzBuzzCounter;
        private long _sequence;
        private ManualResetEvent _signal;

        public FizzBuzzQueueProcessor(FizzBuzzStep fizzBuzzStep,
                                 ConcurrentQueue<long> fizzInputQueue,
                                 ConcurrentQueue<long> buzzInputQueue,
                                 ConcurrentQueue<bool> fizzOutputQueue,
                                 ConcurrentQueue<bool> buzzOutputQueue,
                                 long count)
        {
            _fizzBuzzStep = fizzBuzzStep;

            _fizzInputQueue = fizzInputQueue;
            _buzzInputQueue = buzzInputQueue;
            _fizzOutputQueue = fizzOutputQueue;
            _buzzOutputQueue = buzzOutputQueue;
            _count = count;
        }

        public long FizzBuzzCounter => _fizzBuzzCounter;

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
                        while (!_fizzInputQueue.TryDequeue(out value))
                        {
                            if (!_running)
                                return;
                            Thread.Yield();
                        }
                        _fizzOutputQueue.Enqueue(0 == (value % 3));
                        break;
                    }
                    
                    case FizzBuzzStep.Buzz:
                    {

                        long value;
                        while (!_buzzInputQueue.TryDequeue(out value))
                        {
                            if (!_running)
                                return;
                            Thread.Yield();
                        }
                        _buzzOutputQueue.Enqueue(0 == (value % 5));
                        break;
                    }

                    case FizzBuzzStep.FizzBuzz:
                    {
                        bool fizz;
                        bool buzz;
                        while (!_fizzOutputQueue.TryDequeue(out fizz))
                        {
                            if (!_running)
                                return;
                            Thread.Yield();
                        }
                        while (!_buzzOutputQueue.TryDequeue(out buzz))
                        {
                            if (!_running)
                                return;
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