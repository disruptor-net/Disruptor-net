#if NET8_0_OR_GREATER
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.Tests.IpcPublisher;
using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture]
public class IpcDisruptorStressTest
{
    [Test]
    public async Task ShouldHandleLotsOfThreads()
    {
        await using var disruptor = new IpcDisruptor<IpcStressTestEvent>(65_536, new BusySpinWaitStrategy());

        var iterations = 200000;
        var handlerCount = Math.Clamp(Environment.ProcessorCount / 2, 1, 8);

        var handlers = new TestEventHandler[handlerCount];
        for (var i = 0; i < handlers.Length; i++)
        {
            var handler = new TestEventHandler();
            disruptor.HandleEventsWith(handler);
            handlers[i] = handler;
        }

        var startMutexName = $"Ipc-{Path.GetRandomFileName()}";
        using var startMutex = new Mutex(true, startMutexName);

        var publisher = RemoteIpcPublisher.Start("stress-test", $"--ipc-directory-path {disruptor.IpcDirectoryPath} --iterations {iterations} --mutex-name {startMutexName}");

        Thread.Sleep(500);

        _ = disruptor.Start();

        startMutex.ReleaseMutex();

        Assert.That(publisher.WaitForExit(10000));

        await disruptor.Shutdown();

        Assert.That(publisher.ExitCode, Is.EqualTo(0));

        foreach (var handler in handlers)
        {
            Assert.That(handler.MessagesSeen, Is.Not.EqualTo(0));
            Assert.That(handler.FailureCount, Is.EqualTo(0));
        }
    }

    private class TestEventHandler : IValueEventHandler<IpcStressTestEvent>
    {
        public int FailureCount { get; private set; }
        public int MessagesSeen { get; private set; }

        public void OnEvent(ref IpcStressTestEvent @event, long sequence, bool endOfBatch)
        {
            if (@event.Sequence != sequence || @event.A != sequence + 13 || @event.B != sequence - 7)
            {
                FailureCount++;
            }

            MessagesSeen++;
        }
    }
}
#endif
