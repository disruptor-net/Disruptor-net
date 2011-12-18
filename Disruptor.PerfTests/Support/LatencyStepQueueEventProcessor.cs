using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Disruptor.Collections;

namespace Disruptor.PerfTests.Support
{
    public class LatencyStepQueueEventProcessor
    {
        private readonly FunctionStep _functionStep;

        private readonly BlockingCollection<long> _inputQueue;
        private readonly BlockingCollection<long> _outputQueue;
        private readonly Histogram _histogram;
        private readonly long _nanoTimeCost;
        private readonly double _ticksToNanos;
        private readonly long _iterations;
        private volatile bool _done;

        public LatencyStepQueueEventProcessor(FunctionStep functionStep,
                                    BlockingCollection<long> inputQueue,
                                    BlockingCollection<long> outputQueue,
                                    Histogram histogram, long nanoTimeCost, double ticksToNanos, long iterations)
        {
            _functionStep = functionStep;
            _inputQueue = inputQueue;
            _outputQueue = outputQueue;
            _histogram = histogram;
            _nanoTimeCost = nanoTimeCost;
            _ticksToNanos = ticksToNanos;
            _iterations = iterations;
        }

        public void Reset()
        {
            _done = false;
        }
        
        public bool Done
        {
            get { return _done; }
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
                        case FunctionStep.Two:
                            {
                                _outputQueue.Add(_inputQueue.Take());
                                break;
                            }

                        case FunctionStep.Three:
                            {
                                var value = _inputQueue.Take();
                                var duration = (Stopwatch.GetTimestamp() - value) * _ticksToNanos;
                                duration /= 3;
                                duration -= _nanoTimeCost;
                                _histogram.AddObservation((long)duration);
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
