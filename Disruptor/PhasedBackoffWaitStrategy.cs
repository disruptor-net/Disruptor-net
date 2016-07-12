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
    /// 
    /// TODO Create two versions, one that uses the precise system time (100 nano precision) and another that uses DateTime.UtcNow (1ms precision)
    /// </summary>
    public class PhasedBackoffWaitStrategy : IWaitStrategy
    {
        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern void GetSystemTimePreciseAsFileTime(out long filetime);

        private const int _spinTries = 10000;
        private readonly IWaitStrategy _fallbackStrategy;
        private readonly long _spinDurationInTicks;
        private readonly long _yieldDurationInTicks;

        public PhasedBackoffWaitStrategy(TimeSpan spinDuration, TimeSpan yieldDuration, IWaitStrategy fallbackStrategy)
        {
            _fallbackStrategy = fallbackStrategy;
            _spinDurationInTicks = spinDuration.Ticks;
            _yieldDurationInTicks = yieldDuration.Ticks;

            try
            {
                long fileTime;
                GetSystemTimePreciseAsFileTime(out fileTime);
            }
            catch (EntryPointNotFoundException)
            {
                // Not running Windows 8 or higher.
                throw new ApplicationException("Precise system time is not available so sub-millisecond resolution is not available and this wait strategy is not recommended");
            }
        }
        
        /// <summary>
        /// Block with wait/notifyAll semantics
        /// </summary>
        /// <param name="spinTimeout"></param>
        /// <param name="yieldTimeout"></param>
        /// <param name="units"></param>
        /// <returns></returns>
        public static PhasedBackoffWaitStrategy WithLock(TimeSpan spinTimeout, TimeSpan yieldTimeout)
        {
            return new PhasedBackoffWaitStrategy(spinTimeout, yieldTimeout, new BlockingWaitStrategy());
        }

        /// <summary>
        /// Block by sleeping in a loop
        /// </summary>
        /// <param name="spinTimeout"></param>
        /// <param name="yieldTimeout"></param>
        /// <returns></returns>
        public static PhasedBackoffWaitStrategy WithSleep(TimeSpan spinTimeout, TimeSpan yieldTimeout)
        {
            return new PhasedBackoffWaitStrategy(spinTimeout, yieldTimeout, new SleepingWaitStrategy(0));
        }

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
                        GetSystemTimePreciseAsFileTime(out startTime);
                    }
                    else
                    {
                        long fileTime;
                        GetSystemTimePreciseAsFileTime(out fileTime);
                        var timeDelta = fileTime - startTime;
                        if (timeDelta > _yieldDurationInTicks)
                            return _fallbackStrategy.WaitFor(sequence, cursor, dependentSequence, barrier);

                        if (timeDelta > _spinDurationInTicks)
                            Thread.Yield();
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
    }
}