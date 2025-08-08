#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Diagnostics;
using ConsoleAppFramework;
using Disruptor;
using Disruptor.Tests.Support;

var app = ConsoleApp.Create();
app.Add<Commands>();
app.Run(args);

public class Commands
{
    public void PublishEvents(string ipcDirectoryPath)
    {
        using var memory = IpcRingBufferMemory.Open<StubUnmanagedEvent>(ipcDirectoryPath);
        var publisher = new IpcPublisher<StubUnmanagedEvent>(memory);

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
        using var memory = IpcRingBufferMemory.Open<StubUnmanagedEvent>(ipcDirectoryPath);
        var publisher = new IpcPublisher<StubUnmanagedEvent>(memory);

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
        using var memory = IpcRingBufferMemory.Open<StubUnmanagedEvent>(ipcDirectoryPath);
        var publisher = new IpcPublisher<StubUnmanagedEvent>(memory);

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
}
#else
Console.Error.WriteLine($"IpcPublisher is only supported on .NET 8 and above.");
return 1;
#endif
