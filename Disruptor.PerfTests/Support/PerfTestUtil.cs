using System;

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

        public static void FailIf(long a, long b)
        {
            if (a == b)
            {
                throw new Exception();
            }
        }

        public static void FailIfNot(long a, long b)
        {
            if (a != b)
            {
                throw new Exception();
            }
        }
    }
}