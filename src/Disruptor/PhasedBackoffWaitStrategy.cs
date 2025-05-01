using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
        return new PhasedBackoffWaitStrategy(spinTimeout, yieldTimeout, new SleepingWaitStrategy(0, 0));
    }

    public ISequenceWaiter NewSequenceWaiter(SequenceWaiterOwner owner, DependentSequenceGroup dependentSequences)
    {
        var fallback = _fallbackStrategy.NewSequenceWaiter(owner, dependentSequences);

        return new SequenceWaiter(dependentSequences, fallback, _spinTimeout, _yieldTimeout);
    }

    public void SignalAllWhenBlocking()
    {
        _fallbackStrategy.SignalAllWhenBlocking();
    }

    private static long ToStopwatchTimeout(TimeSpan timeout)
    {
        return (long)(timeout.Ticks * (double)Stopwatch.Frequency / TimeSpan.TicksPerSecond);
    }

    private class SequenceWaiter : ISequenceWaiter
    {
        private readonly DependentSequenceGroup _dependentSequences;
        private readonly ISequenceWaiter _fallback;
        private readonly long _spinTimeout;
        private readonly long _yieldTimeout;

        public SequenceWaiter(DependentSequenceGroup dependentSequences, ISequenceWaiter fallback, long spinTimeout, long yieldTimeout)
        {
            _dependentSequences = dependentSequences;
            _fallback = fallback;
            _spinTimeout = spinTimeout;
            _yieldTimeout = yieldTimeout;
        }

        public DependentSequenceGroup DependentSequences => _dependentSequences;

        public SequenceWaitResult WaitFor(long sequence, CancellationToken cancellationToken)
        {
            var startTime = 0L;
            var counter = _spinTries;
            long availableSequence;

            while ((availableSequence = _dependentSequences.Value) < sequence)
            {
                cancellationToken.ThrowIfCancellationRequested();

                counter--;

                if (counter == 0)
                {
                    if (startTime == 0)
                    {
                        startTime = Stopwatch.GetTimestamp();
                    }
                    else
                    {
                        var timeDelta = Stopwatch.GetTimestamp() - startTime;
                        if (timeDelta > _yieldTimeout)
                        {
                            return _fallback.WaitFor(sequence, cancellationToken);
                        }

                        if (timeDelta > _spinTimeout)
                        {
                            Thread.Yield();
                        }
                    }

                    counter = _spinTries;
                }
            }

            return availableSequence;
        }

        public void Cancel()
        {
            _fallback.Cancel();
        }

        public void Dispose()
        {
            _fallback.Dispose();
        }
    }
}
