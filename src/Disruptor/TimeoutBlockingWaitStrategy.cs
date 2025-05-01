using System;
using System.Threading;
using Disruptor.Processing;

namespace Disruptor;

/// <summary>
/// Blocking wait strategy that uses <c>Monitor.Wait</c> and <c>Monitor.PulseAll</c>.
/// If the awaited sequence is not available after the configured timeout, the strategy returns <see cref="SequenceWaitResult.Timeout"/>.
/// </summary>
/// <remarks>
/// Using a timeout wait strategy is only useful if your event handler handles timeouts (<see cref="IEventHandler.OnTimeout"/>).
///
/// This strategy can be used when throughput and low-latency are not as important as CPU resources.
/// </remarks>
public sealed class TimeoutBlockingWaitStrategy : IWaitStrategy
{
    private readonly object _gate = new();
    private readonly int _timeoutMilliseconds;

    public TimeoutBlockingWaitStrategy(TimeSpan timeout)
    {
        var totalMilliseconds = (long)timeout.TotalMilliseconds;
        if (totalMilliseconds is < 0 or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        _timeoutMilliseconds = (int)totalMilliseconds;
    }

    public bool IsBlockingStrategy => true;

    public ISequenceWaiter NewSequenceWaiter(IEventHandler? eventHandler, DependentSequenceGroup dependentSequences)
    {
        return new SequenceWaiter(_gate, _timeoutMilliseconds, dependentSequences);
    }

    public void SignalAllWhenBlocking()
    {
        lock (_gate)
        {
            Monitor.PulseAll(_gate);
        }
    }

    private class SequenceWaiter(object gate, int timeoutMilliseconds, DependentSequenceGroup dependentSequences) : ISequenceWaiter
    {
        public DependentSequenceGroup DependentSequences => dependentSequences;

        public SequenceWaitResult WaitFor(long sequence, CancellationToken cancellationToken)
        {
            var timeout = timeoutMilliseconds;
            if (dependentSequences.CursorValue < sequence)
            {
                lock (gate)
                {
                    while (dependentSequences.CursorValue < sequence)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!Monitor.Wait(gate, timeout))
                        {
                            return SequenceWaitResult.Timeout;
                        }
                    }
                }
            }

            return dependentSequences.AggressiveSpinWaitFor(sequence, cancellationToken);
        }

        public void Cancel()
        {
            lock (gate)
            {
                Monitor.PulseAll(gate);
            }
        }

        public void Dispose()
        {
        }
    }
}
