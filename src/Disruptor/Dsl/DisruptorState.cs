using System;
using System.Threading;

namespace Disruptor.Dsl;

/// <summary>
/// Utility type that encapsulates the running state of a disruptor.
/// </summary>
internal class DisruptorState
{
    private const int Created = 0;
    private const int Started = 1;
    private const int Halted = 2;

    private volatile int _state;

    public bool HasStarted => _state >= Started;
    public bool IsRunning => _state == Started;

    public void ThrowIfStarted()
    {
        if (HasStarted)
        {
            throw new InvalidOperationException("The disruptor cannot be configured after start.");
        }
    }

    public void Start()
    {
        var previousState = Interlocked.CompareExchange(ref _state, Started, Created);
        if (previousState == Started)
        {
            throw new InvalidOperationException($"The disruptor is already started.");
        }

        if (previousState == Halted)
        {
            throw new InvalidOperationException($"The disruptor halted and cannot be restarted.");
        }
    }

    public void Halt()
    {
        var previousState = Interlocked.CompareExchange(ref _state, Halted, Started);
        if (previousState == Created)
        {
            throw new InvalidOperationException($"The disruptor is not started.");
        }

        if (previousState == Halted)
        {
            throw new InvalidOperationException($"The disruptor is already halted.");
        }
    }

    public override string ToString()
    {
        return _state switch
        {
            Created => "Created",
            Started => "Running",
            Halted  => "Halted",
            _       => "Unknown"
        };
    }
}
