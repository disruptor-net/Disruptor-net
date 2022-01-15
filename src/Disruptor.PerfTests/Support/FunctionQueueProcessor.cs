using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor.PerfTests.Support;

public class FunctionQueueProcessor
{
    private readonly FunctionStep _functionStep;
    private readonly ConcurrentQueue<long[]> _stepOneQueue;
    private readonly ConcurrentQueue<long> _stepTwoQueue;
    private readonly ConcurrentQueue<long> _stepThreeQueue;
    private readonly long _count;

    private volatile bool _running;
    private long _stepThreeCounter;
    private long _sequence;
    private ManualResetEvent _signal;

    public FunctionQueueProcessor(FunctionStep functionStep,
                                  ConcurrentQueue<long[]> stepOneQueue,
                                  ConcurrentQueue<long> stepTwoQueue,
                                  ConcurrentQueue<long> stepThreeQueue,
                                  long count)
    {
        _functionStep = functionStep;
        _stepOneQueue = stepOneQueue;
        _stepTwoQueue = stepTwoQueue;
        _stepThreeQueue = stepThreeQueue;
        _count = count;
    }

    public long StepThreeCounter => _stepThreeCounter;

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
                    while (!_stepOneQueue.TryDequeue(out values))
                    {
                        if (!_running)
                            return;
                        Thread.Yield();
                    }
                    _stepTwoQueue.Enqueue(values[0] + values[1]);
                    break;
                }
                case FunctionStep.Two:
                {
                    long value;
                    while (!_stepTwoQueue.TryDequeue(out value))
                    {
                        if (!_running)
                            return;
                        Thread.Yield();
                    }
                    _stepThreeQueue.Enqueue(value + 3);
                    break;
                }
                case FunctionStep.Three:
                {
                    long value;
                    while (!_stepThreeQueue.TryDequeue(out value))
                    {
                        if (!_running)
                            return;
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

    public Task Start()
    {
        return PerfTestUtil.StartLongRunning(Run);
    }
}