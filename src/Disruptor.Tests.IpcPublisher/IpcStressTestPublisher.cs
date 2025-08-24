namespace Disruptor.Tests.IpcPublisher;

public struct IpcStressTestEvent
{
    public long Sequence;
    public long A;
    public long B;
}

internal class IpcStressTestPublisher
{
    private readonly IpcPublisher<IpcStressTestEvent> _ringBuffer;
    private readonly CountdownEvent _end;
    private readonly CountdownEvent _start;
    private readonly int _iterations;

    public bool Failed;

    public IpcStressTestPublisher(IpcPublisher<IpcStressTestEvent> ringBuffer, int iterations, CountdownEvent start, CountdownEvent end)
    {
        _ringBuffer = ringBuffer;
        _end = end;
        _start = start;
        _iterations = iterations;
    }

    public void Run()
    {
        try
        {
            _start.Signal();
            _start.Wait();

            var i = _iterations;
            while (--i != -1)
            {
                var next = _ringBuffer.Next();
                ref var testEvent = ref _ringBuffer[next];
                testEvent.Sequence = next;
                testEvent.A = next + 13;
                testEvent.B = next - 7;
                _ringBuffer.Publish(next);
            }
        }
        catch (Exception)
        {
            Failed = true;
        }
        finally
        {
            _end.Signal();
        }
    }
}
