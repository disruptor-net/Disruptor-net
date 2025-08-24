#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Diagnostics;
using ConsoleAppFramework;
using Disruptor;
using Disruptor.PerfTests.Support;
using Disruptor.Tests.IpcPublisher;
using Disruptor.Tests.Support;

var app = ConsoleApp.Create();
app.Add<Commands>();
app.Run(args);

public class Commands
{
    public void PublishEvents(string ipcDirectoryPath)
    {
        using var publisher = new IpcPublisher<StubUnmanagedEvent>(ipcDirectoryPath);

        using (var scope = publisher.PublishEvent())
        {
            scope.Event().Key = 101;
            scope.Event().Value = (int)scope.Sequence;
        }

        using (var scope = publisher.PublishEvent())
        {
            scope.Event().Key = 102;
            scope.Event().Value = (int)scope.Sequence;
        }
    }

    public void PublishEventsBatch(string ipcDirectoryPath)
    {
        using var publisher = new IpcPublisher<StubUnmanagedEvent>(ipcDirectoryPath);

        using (var scope = publisher.PublishEvents(2))
        {
            scope.Event(0).Key = 101;
            scope.Event(0).Value = (int)scope.StartSequence;
            scope.Event(1).Key = 102;
            scope.Event(1).Value = (int)scope.StartSequence + 1;
        }

        using (var scope = publisher.PublishEvents(2))
        {
            scope.Event(0).Key = 103;
            scope.Event(0).Value = (int)scope.StartSequence;
            scope.Event(1).Key = 104;
            scope.Event(1).Value = (int)scope.StartSequence + 1;
        }
    }

    public void PublishManyEvents(string ipcDirectoryPath, int eventCount, int key = 101, string? mutexName = null)
    {
        using var publisher = new IpcPublisher<StubUnmanagedEvent>(ipcDirectoryPath);

        if (mutexName != null)
        {
            Console.WriteLine($"RemoteIpcPublisher || {nameof(PublishManyEvents)} || Waiting for mutex, Name: {mutexName}");

            using var mutex = Mutex.OpenExisting(mutexName);
            mutex.WaitOne();
            mutex.ReleaseMutex();
        }

        Console.WriteLine($"RemoteIpcPublisher || {nameof(PublishManyEvents)} || Starting publication, EventCount: {eventCount}");

        for (var i = 0; i < eventCount; i++)
        {
            using (var scope = publisher.PublishEvent())
            {
                scope.Event().Value = i;
                scope.Event().Key = key;
            }
        }
    }

    public int TryCreateRingBuffer(string ipcDirectoryPath)
    {
        try
        {
            using var memory = IpcRingBufferMemory.Open<StubUnmanagedEvent>(ipcDirectoryPath);
            using var ringBuffer = new IpcRingBuffer<StubUnmanagedEvent>(memory, new BusySpinWaitStrategy(), false);
        }
        catch (InvalidOperationException)
        {
            return 0; // OK
        }
        catch (Exception)
        {
            return 2;
        }

        return 1;
    }

    public void ReadMinimumGatingSequence(string ipcDirectoryPath, long expectedValue)
    {
        using var publisher = new IpcPublisher<PerfValueEvent>(ipcDirectoryPath);

        var minimumGatingSequence = publisher.GetMinimumGatingSequence();
        if (minimumGatingSequence != expectedValue)
            throw new InvalidOperationException($"Invalid minimum gating sequence, expected {expectedValue} but was {minimumGatingSequence}");
    }

    public void ThroughputTest(string ipcDirectoryPath, int iterations, string mutexName, int? cpu)
    {
        using var _ = ThreadAffinityUtil.SetThreadAffinity(cpu, ThreadPriority.Highest);

        using var publisher = new IpcPublisher<PerfValueEvent>(ipcDirectoryPath);

        using var mutex = Mutex.OpenExisting(mutexName);
        mutex.WaitOne();

        for (long i = 0; i < iterations; i++)
        {
            var sequence = publisher.Next();
            publisher[sequence].Value = i;
            publisher.Publish(sequence);
        }
    }

    public int StressTest(string ipcDirectoryPath, int iterations, string mutexName)
    {
        using var publisher = new IpcPublisher<IpcStressTestEvent>(ipcDirectoryPath);

        var publisherCount = Math.Clamp(Environment.ProcessorCount / 2, 1, 8);

        var start = new CountdownEvent(publisherCount);
        var end = new CountdownEvent(publisherCount);

        var testPublishers = new IpcStressTestPublisher[publisherCount];
        for (var i = 0; i < testPublishers.Length; i++)
        {
            testPublishers[i] = new IpcStressTestPublisher(publisher, iterations, start, end);
        }

        using var mutex = Mutex.OpenExisting(mutexName);
        mutex.WaitOne();

        foreach (var testPublisher in testPublishers)
        {
            Task.Run(testPublisher.Run);
        }

        end.Wait();

        var spinWait = new SpinWait();

        while (publisher.Cursor < (iterations - 1))
        {
            spinWait.SpinOnce();
        }

        return testPublishers.Any(x => x.Failed) ? 1 : 0;
    }
}
#else
Console.Error.WriteLine($"IpcPublisher is only supported on .NET 8 and above.");
return 1;
#endif
