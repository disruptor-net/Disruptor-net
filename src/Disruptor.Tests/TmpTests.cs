using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.Util;
using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture, Explicit("Manual")]
public class TmpTests
{
    [Test]
    public void METHOD()
    {
        using var scope = TimerResolutionUtil.SetTimerResolution(1);
        var obj = new object();

        for (var i = 0; i < 100; i++)
        {
            // Thread.Sleep(1);
            lock (obj)
            {
                Monitor.Wait(obj, 1);
            }

            Console.WriteLine($"Timeout! {DateTime.UtcNow:HH:mm:ss.fff}");
        }
    }

    [Test]
    public void AsyncTimeoutTest()
    {
        var disruptor = new Disruptor<XEvent>(() => new XEvent(), 1024, new TimeoutAsyncWaitStrategy(TimeSpan.FromMilliseconds(50)));
        disruptor.HandleEventsWith(new XHandler());
        disruptor.Start();

        Thread.Sleep(TimeSpan.FromSeconds(5.1));

        disruptor.Halt();
    }

    private class XEvent
    {
        public int Id { get; set; }
    }

    private class XHandler : IAsyncBatchEventHandler<XEvent>
    {
        public ValueTask OnBatch(EventBatch<XEvent> batch, long sequence)
        {
            return ValueTask.CompletedTask;
        }

        public void OnTimeout(long sequence)
        {
            Console.WriteLine($"Timeout! {DateTime.UtcNow:HH:mm:ss.fff}");
        }
    }
}
