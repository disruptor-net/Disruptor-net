using System;
using System.Threading;

#if NET8_0_OR_GREATER
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
            scope.Event().Value = (int)scope.Sequence;
            scope.Event().Key = 101;
        }

        using (var scope = publisher.PublishEvent())
        {
            scope.Event().Value = (int)scope.Sequence;
            scope.Event().Key = 102;
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
Console.Error.WriteLine($"IpcPublisher only supported on .NET 8 and above.");
return 1;
#endif
