using System;
using System.Diagnostics;
using System.Threading;

namespace Disruptor;

/// <summary>
/// Phased wait strategy for waiting event processors on a barrier.
/// Spins, then yields, then waits using the configured fallback <see cref="IWaitStrategy"/>.
/// </summary>
/// <remarks>
/// This strategy can be used when throughput and low-latency are not as important as CPU resources.
/// </remarks>
public sealed class PhasedBackoffWaitStrategy : IWaitStrategy
{
    private const int _spinTries = 10000;
    private readonly IWaitStrategy _fallbackStrategy;
    private readonly long _spinTimeout;
    private readonly long _yieldTimeout;

    public PhasedBackoffWaitStrategy(TimeSpan spinTimeout, TimeSpan yieldTimeout, IWaitStrategy fallbackStrategy)
    {
        _spinTimeout = ToStopwatchTimeout(spinTimeout);
        _yieldTimeout = ToStopwatchTimeout(yieldTimeout);
        _fallbackStrategy = fallbackStrategy;
    }

    public bool IsBlockingStrategy => _fallbackStrategy.IsBlockingStrategy;
    public long SpinTimeout => _spinTimeout;
    public long YieldTimeout => _yieldTimeout;

    /// <summary>
    /// Construct <see cref="PhasedBackoffWaitStrategy"/> with fallback to <see cref="BlockingWaitStrategy"/>
    /// </summary>
    /// <param name="spinTimeout">The maximum time in to busy spin for.</param>
    /// <param name="yieldTimeout">The maximum time in to yield for.</param>
    /// <returns>The constructed wait strategy.</returns>
    public static PhasedBackoffWaitStrategy WithLock(TimeSpan spinTimeout, TimeSpan yieldTimeout)
    {
        return new PhasedBackoffWaitStrategy(spinTimeout, yieldTimeout, new BlockingWaitStrategy());
    }

    /// <summary>
    /// Construct <see cref="PhasedBackoffWaitStrategy"/> with fallback to <see cref="SleepingWaitStrategy"/>
    /// </summary>
    /// <param name="spinTimeout">The maximum time in to busy spin for.</param>
    /// <param name="yieldTimeout">The maximum time in to yield for.</param>
    /// <returns>The constructed wait strategy.</returns>
    public static PhasedBackoffWaitStrategy WithSleep(TimeSpan spinTimeout, TimeSpan yieldTimeout)
    {
        return new PhasedBackoffWaitStrategy(spinTimeout, yieldTimeout, new SleepingWaitStrategy(0));
    }

    public SequenceWaitResult WaitFor(long sequence, DependentSequenceGroup dependentSequences, CancellationToken cancellationToken)
    {
        long startTime = 0;
        int counter = _spinTries;

        do
        {
            long availableSequence;
            if ((availableSequence = dependentSequences.Value) >= sequence)
                return availableSequence;

            if (0 == --counter)
            {
                if (0 == startTime)
                {
                    startTime = Stopwatch.GetTimestamp();
                }
                else
                {
                    var timeDelta = Stopwatch.GetTimestamp() - startTime;
                    if (timeDelta > _yieldTimeout)
                    {
                        return _fallbackStrategy.WaitFor(sequence, dependentSequences, cancellationToken);
                    }

                    if (timeDelta > _spinTimeout)
                    {
                        Thread.Yield();
                    }
                }

                counter = _spinTries;
            }
        }
        while (true);
    }

    public void SignalAllWhenBlocking()
    {
        _fallbackStrategy.SignalAllWhenBlocking();
    }

    private static long ToStopwatchTimeout(TimeSpan timeout)
    {
        return (long)(timeout.Ticks * (double)Stopwatch.Frequency / TimeSpan.TicksPerSecond);
    }
}
