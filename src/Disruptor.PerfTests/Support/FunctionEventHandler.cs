using System.Threading;
using Disruptor.Testing.Support;

namespace Disruptor.PerfTests.Support;

public class FunctionEventHandler : IEventHandler<FunctionEvent>
{
    private readonly FunctionStep _functionStep;
    private PaddedLong _counter;
    private long _iterations;
    private ManualResetEvent _latch;

    public long StepThreeCounter => _counter.Value;

    public FunctionEventHandler(FunctionStep functionStep)
    {
        _functionStep = functionStep;
    }

    public void Reset(ManualResetEvent latch, long iterations)
    {
        _counter.Value = 0;
        _iterations = iterations;
        _latch = latch;
    }

    public void OnEvent(FunctionEvent data, long sequence, bool endOfBatch)
    {
        switch (_functionStep)
        {
            case FunctionStep.One:
                data.StepOneResult = data.OperandOne + data.OperandTwo;
                break;
            case FunctionStep.Two:
                data.StepTwoResult = data.StepOneResult + 3L;
                break;

            case FunctionStep.Three:
                if ((data.StepTwoResult & 4L) == 4L)
                {
                    _counter.Value = _counter.Value + 1;
                }
                break;
        }

        if(sequence == _iterations-1)
        {
            _latch.Set();
        }
    }
}
