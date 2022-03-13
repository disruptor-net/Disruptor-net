using System;
using System.Threading;

namespace Disruptor;

/// <summary>
/// Variation of the <see cref="TimeoutBlockingWaitStrategy"/> that attempts to elide conditional wake-ups
/// when the lock is uncontended.
/// </summary>
public sealed class LiteTimeoutBlockingWaitStrategy : IWaitStrategy
{
    private readonly object _lock = new();
    private volatile int _signalNeeded;
    private readonly int _timeoutInMilliseconds;

    public LiteTimeoutBlockingWaitStrategy(TimeSpan timeout)
    {
        _timeoutInMilliseconds = (int)timeout.TotalMilliseconds;
    }

    public bool IsBlockingStrategy => true;

    public SequenceWaitResult WaitFor(long sequence, DependentSequenceGroup dependentSequences, CancellationToken cancellationToken)
    {
        var milliseconds = _timeoutInMilliseconds;

        if (dependentSequences.CursorValue < sequence)
        {
            lock (_lock)
            {
                while (dependentSequences.CursorValue < sequence)
                {
                    Interlocked.Exchange(ref _signalNeeded, 1);

                    cancellationToken.ThrowIfCancellationRequested();

                    if (!Monitor.Wait(_lock, milliseconds))
                    {
                        return SequenceWaitResult.Timeout;
                    }
                }
            }
        }

        return dependentSequences.AggressiveSpinWaitFor(sequence, cancellationToken);
    }

    public void SignalAllWhenBlocking()
    {
        if (Interlocked.Exchange(ref _signalNeeded, 0) == 1)
        {
            lock (_lock)
            {
                Monitor.PulseAll(_lock);
            }
        }
    }
}
