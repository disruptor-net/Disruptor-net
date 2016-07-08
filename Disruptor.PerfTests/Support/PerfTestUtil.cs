using System;
using System.Threading;

namespace Disruptor.PerfTests.Support
{
    public class PerfTestUtil
    {
        public static long AccumulatedAddition(long iterations)
        {
            long temp = 0L;
            for (long i = 0L; i < iterations; i++)
            {
                temp += i;
            }

            return temp;
        }

        public static void FailIf(long a, long b, string message)
        {
            if (a == b)
            {
                throw new Exception(message);
            }
        }

        public static void FailIfNot(long a, long b, string message)
        {
            if (a != b)
            {
                throw new Exception(message);
            }
        }

        public static void WaitForEventProcessorSequence(long expectedCount, IEventProcessor batchEventProcessor)
        {
            while (batchEventProcessor.Sequence.Value != expectedCount)
            {
                Thread.Sleep(1);
            }
        }
    }
}