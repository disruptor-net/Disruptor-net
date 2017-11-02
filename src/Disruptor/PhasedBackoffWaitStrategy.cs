using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// <para>Phased wait strategy for waiting <see cref="IEventProcessor"/>s on a barrier.</para>
    /// 
    /// <para>This strategy can be used when throughput and low-latency are not as important as CPU resource.
    /// Spins, then yields, then waits using the configured fallback WaitStrategy.</para>
    /// </summary>
    public class PhasedBackoffWaitStrategy : IWaitStrategy
    {
        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern void GetSystemTimePreciseAsFileTime(out long filetime);

        private static readonly bool _isSystemTimePreciseAsFileTimeAvailable;

        private const int _spinTries = 10000;
        private readonly IWaitStrategy _fallbackStrategy;
        private readonly long _spinTimeoutTicks;
        private readonly long _yieldTimeoutTicks;

        static PhasedBackoffWaitStrategy()
        {
            try
            {
                long fileTime;
                GetSystemTimePreciseAsFileTime(out fileTime);
                _isSystemTimePreciseAsFileTimeAvailable = true;
            }
            catch (TypeLoadException)
            {
            }
        }

        public PhasedBackoffWaitStrategy(TimeSpan spinTimeout, TimeSpan yieldTimeout, IWaitStrategy fallbackStrategy)
        {
            _spinTimeoutTicks = spinTimeout.Ticks;
            _yieldTimeoutTicks = yieldTimeout.Ticks;
            _fallbackStrategy = fallbackStrategy;
        }

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

        /// <summary>
        /// <see cref="IWaitStrategy.WaitFor"/>
        /// </summary>
        public long WaitFor(long sequence, Sequence cursor, ISequence dependentSequence, ISequenceBarrier barrier)
        {
            long startTime = 0;
            int counter = _spinTries;

            do
            {
                long availableSequence;
                if ((availableSequence = dependentSequence.Value) >= sequence)
                    return availableSequence;

                if (0 == --counter)
                {
                    if (0 == startTime)
                    {
                        startTime = GetSystemTimeTicks();
                    }
                    else
                    {
                        var timeDelta = GetSystemTimeTicks() - startTime;
                        if (timeDelta > _yieldTimeoutTicks)
                        {
                            return _fallbackStrategy.WaitFor(sequence, cursor, dependentSequence, barrier);
                        }

                        if (timeDelta > _spinTimeoutTicks)
                        {
                            Thread.Yield();
                        }
                    }
                    counter = _spinTries;
                }
            }
            while (true);
        }

        /// <summary>
        /// <see cref="IWaitStrategy.SignalAllWhenBlocking"/>
        /// </summary>
        public void SignalAllWhenBlocking()
        {
            _fallbackStrategy.SignalAllWhenBlocking();
        }

        private static long GetSystemTimeTicks()
        {
            long ticks;
            if (_isSystemTimePreciseAsFileTimeAvailable)
            {
                GetSystemTimePreciseAsFileTime(out ticks);
            }
            else
            {
                ticks = DateTime.UtcNow.Ticks;
            }
            return ticks;
        }
    }
}