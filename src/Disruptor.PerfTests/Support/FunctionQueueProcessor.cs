using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Disruptor.PerfTests.Support
{
    public class FunctionQueueProcessor
    {
        private readonly FunctionStep _functionStep;
        private readonly BlockingCollection<long[]> _stepOneQueue;
        private readonly BlockingCollection<long> _stepTwoQueue;
        private readonly BlockingCollection<long> _stepThreeQueue;
        private readonly long _count;

        private volatile bool _running;
        private long _stepThreeCounter;
        private long _sequence;
        private ManualResetEvent _signal;

        public FunctionQueueProcessor(FunctionStep functionStep,
                                      BlockingCollection<long[]> stepOneQueue,
                                      BlockingCollection<long> stepTwoQueue,
                                      BlockingCollection<long> stepThreeQueue,
                                      long count)
        {
            _functionStep = functionStep;
            _stepOneQueue = stepOneQueue;
            _stepTwoQueue = stepTwoQueue;
            _stepThreeQueue = stepThreeQueue;
            _count = count;
        }

        public void Reset(ManualResetEvent signal)
        {
            _stepThreeCounter = 0L;
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
                switch (_functionStep)
                {
                    case FunctionStep.One:
                    {
                        long[] values;
                        while (!_stepOneQueue.TryTake(out values))
                        {
                            if (!_running)
                                break;
                            Thread.Yield();
                        }

                        while (!_stepTwoQueue.TryAdd(values[0] + values[1]))
                        {
                            if (!_running)
                                break;
                            Thread.Yield();
                        }
                        break;
                    }
                    case FunctionStep.Two:
                    {
                        long value;
                        while (!_stepTwoQueue.TryTake(out value))
                        {
                            if (!_running)
                                break;
                            Thread.Yield();
                        }

                        while (!_stepThreeQueue.TryAdd(value + 3))
                        {
                            if (!_running)
                                break;
                            Thread.Yield();
                        }
                        break;
                    }
                    case FunctionStep.Three:
                    {
                        long value;
                        while (!_stepThreeQueue.TryTake(out value))
                        {
                            if (!_running)
                                break;
                            Thread.Yield();
                        }

                        if ((value & 4L) == 4L)
                        {
                            ++_stepThreeCounter;
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