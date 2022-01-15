using System;
using System.Diagnostics;
using System.Threading;

namespace Disruptor.Samples.Wiki.PublicationOptions;

public static class PublicationExtensions
{
    public static long Next(this RingBuffer ringBuffer, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        var spinWait = new SpinWait();

        do
        {
            if (ringBuffer.TryNext(out var sequence))
                return sequence;
                
            spinWait.SpinOnce();
        }
        while (stopwatch.Elapsed < timeout);

        throw new TimeoutException();
    }
}