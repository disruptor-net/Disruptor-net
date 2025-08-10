using System;
using System.Diagnostics;
using System.Threading;

namespace Disruptor;

/// <summary>
/// Non-blocking wait strategy that uses <c>Thread.Yield()</c>.
/// If the awaited sequence is not available after the configured timeout, the strategy returns <see cref="SequenceWaitResult.Timeout"/>.
/// </summary>
/// <remarks>
/// Using a timeout wait strategy is only useful if your event handler handles timeouts (<see cref="IEventHandler.OnTimeout"/>).
/// This strategy is a good compromise between performance and CPU resources without incurring significant latency spikes.
/// </remarks>
public sealed class TimeoutYieldingWaitStrategy : IWaitStrategy, IIpcWaitStrategy
{
    private readonly long _timeout;
    private readonly int _yieldIndex;

    public TimeoutYieldingWaitStrategy(TimeSpan timeout)
        : this(timeout, 100)
    {
    }

    public TimeoutYieldingWaitStrategy(TimeSpan timeout, int busySpinCount)
    {
        _timeout = (long)(timeout.Ticks * (double)Stopwatch.Frequency / TimeSpan.TicksPerSecond);
        _yieldIndex = busySpinCount;
    }

    public bool IsBlockingStrategy => false;

    public ISequenceWaiter NewSequenceWaiter(SequenceWaiterOwner owner, DependentSequenceGroup dependentSequences)
    {
        return new SequenceWaiter(dependentSequences, _timeout, _yieldIndex);
    }

    public void SignalAllWhenBlocking()
    {
    }

    public IIpcSequenceWaiter NewSequenceWaiter(SequenceWaiterOwner owner, IpcDependentSequenceGroup dependentSequences)
    {
        return new IpcSequenceWaiter(dependentSequences, _timeout, _yieldIndex);
    }

    private class SequenceWaiter(DependentSequenceGroup dependentSequences, long timeout, int yieldIndex) : ISequenceWaiter
    {
        public SequenceWaitResult WaitFor(long sequence, CancellationToken cancellationToken)
        {
            var timeoutTimestamp = Stopwatch.GetTimestamp() + timeout;
            long availableSequence;
            var counter = 0;

            while ((availableSequence = dependentSequences.Value) < sequence)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Stopwatch.GetTimestamp() >= timeoutTimestamp)
                {
                    return SequenceWaitResult.Timeout;
                }

                if (counter >= yieldIndex)
                {
                    Thread.Yield();
                }

                counter++;
            }

            return availableSequence;
        }

        public void Cancel()
        {
        }

        public void Dispose()
        {
        }
    }

    private class IpcSequenceWaiter(IpcDependentSequenceGroup dependentSequences, long timeout, int yieldIndex) : IIpcSequenceWaiter
    {
        public SequenceWaitResult WaitFor(long sequence, CancellationToken cancellationToken)
        {
            var timeoutTimestamp = Stopwatch.GetTimestamp() + timeout;
            long availableSequence;
            var counter = 0;

            while ((availableSequence = dependentSequences.Value) < sequence)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Stopwatch.GetTimestamp() >= timeoutTimestamp)
                {
                    return SequenceWaitResult.Timeout;
                }

                if (counter >= yieldIndex)
                {
                    Thread.Yield();
                }

                counter++;
            }

            return availableSequence;
        }
    }
}
