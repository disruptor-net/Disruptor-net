using System;
using System.Collections.Concurrent;

namespace Disruptor.PerfTests.Support
{
    public class FunctionQueueEventProcessor
    {
        private readonly FunctionStep _functionStep;
        private readonly BlockingCollection<long[]> _stepOneQueue;
        private readonly BlockingCollection<long> _stepTwoQueue;
        private readonly BlockingCollection<long> _stepThreeQueue;
        private readonly long _iterations;
        private long _stepThreeCounter;
        private volatile bool _done;

        public FunctionQueueEventProcessor(FunctionStep functionStep,
                                 BlockingCollection<long[]> stepOneQueue,
                                 BlockingCollection<long> stepTwoQueue,
                                 BlockingCollection<long> stepThreeQueue,
                                 long iterations)
        {
            _functionStep = functionStep;
            _stepOneQueue = stepOneQueue;
            _stepTwoQueue = stepTwoQueue;
            _stepThreeQueue = stepThreeQueue;
            _iterations = iterations;
        }

        public long StepThreeCounter
        {
            get
            {
                return _stepThreeCounter;    
            }
        }

        public bool Done
        {
            get 
            {
                return _done;
            }
        }

        public void Reset()
        {
            _stepThreeCounter = 0L;
            _done = false;
        }

        public void Run()
        {
            for (var i = 0; i < _iterations; i++)
            {
                try
                {
                    switch (_functionStep)
                    {
                        case FunctionStep.One:
                            {
                                var values = _stepOneQueue.Take();
                                _stepTwoQueue.Add(values[0] + values[1]);
                                break;
                            }

                        case FunctionStep.Two:
                            {
                                var value = _stepTwoQueue.Take();
                                _stepThreeQueue.Add(value + 3);
                                break;
                            }

                        case FunctionStep.Three:
                            {
                                var value = _stepThreeQueue.Take();
                                var testValue = value;
                                if ((testValue & 4L) == 4L)
                                {
                                    ++_stepThreeCounter;
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