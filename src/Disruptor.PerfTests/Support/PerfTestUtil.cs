using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;

namespace Disruptor.PerfTests.Support;

public class PerfTestUtil
{
    public static Task StartLongRunning(Action action)
    {
        return Task.Factory.StartNew(action, TaskCreationOptions.LongRunning);
    }

    public static long AccumulatedAddition(long iterations)
    {
        long temp = 0L;
        for (long i = 0L; i < iterations; i++)
        {
            temp += i;
        }

        return temp;
    }

    public static void FailIf(long a, long b, string message = null)
    {
        if (a == b)
        {
            throw new Exception(message ?? $"Test failed {a} == {b}");
        }
    }

    public static void FailIfNot(long a, long b, string message = null)
    {
        if (a != b)
        {
            throw new Exception(message ?? $"Test failed {a} != {b}");
        }
    }

    public static void WaitForEventProcessorSequence(long expectedCount, IEventProcessor eventProcessor)
    {
        WaitForEventProcessorSequence(expectedCount, eventProcessor.Sequence);
    }

    public static void WaitForEventProcessorSequence(long expectedCount, Sequence sequence)
    {
        while (sequence.Value != expectedCount)
        {
            Thread.Sleep(1);
        }
    }
}
