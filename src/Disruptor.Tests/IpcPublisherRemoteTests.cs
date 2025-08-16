#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;
using Disruptor.Tests.IpcPublisher;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests;

/// <summary>
/// IpcPublisher tests using a remote (inter-process) IPC publisher.
/// </summary>
[TestFixture]
public class IpcPublisherRemoteTests : IDisposable
{
    private readonly IpcRingBuffer<StubUnmanagedEvent> _ringBuffer;
    private readonly CursorFollower _cursorFollower;

    public IpcPublisherRemoteTests()
    {
        var memory = IpcRingBufferMemory.CreateTemporary(1024, initializer: _ => new StubUnmanagedEvent(-1));
        _ringBuffer = new IpcRingBuffer<StubUnmanagedEvent>(memory, new YieldingWaitStrategy(), true);
        _cursorFollower = CursorFollower.StartNew(_ringBuffer);
        _ringBuffer.SetGatingSequences(_cursorFollower.SequencePointer);
    }

    public void Dispose()
    {
        _cursorFollower.Dispose();
        _ringBuffer.Dispose();
    }

    [Test]
    public void ShouldPublishEvents()
    {
        RunRemotePublisher("publish-events", $"--ipc-directory-path \"{_ringBuffer.IpcDirectoryPath}\"");

        Assert.That(_ringBuffer, IsIpcRingBuffer.WithEvents(new StubUnmanagedEvent(0, 101), new StubUnmanagedEvent(1, 102)));;
    }

    [Test]
    public void ShouldPublishManyEvents()
    {
        RunRemotePublisher("publish-many-events", $"--ipc-directory-path \"{_ringBuffer.IpcDirectoryPath}\" --event-count 500");

        var expectedEvents = Enumerable.Range(0, 500).Select(i => new StubUnmanagedEvent(i, 101)).ToArray();

        Assert.That(_ringBuffer, IsIpcRingBuffer.WithEvents(expectedEvents));
    }

    [Test]
    public void ShouldPublishManyEventsFromMultiplePublishers()
    {
        var mutexName = $"Ipc-{Path.GetRandomFileName()}";
        using var mutex = new Mutex(true, mutexName);

        var p1 = Task.Run(() => RunRemotePublisher("publish-many-events", $"--ipc-directory-path \"{_ringBuffer.IpcDirectoryPath}\" --event-count 500 --key 101 --mutex-name \"{mutexName}\""));
        var p2 = Task.Run(() => RunRemotePublisher("publish-many-events", $"--ipc-directory-path \"{_ringBuffer.IpcDirectoryPath}\" --event-count 500 --key 102 --mutex-name \"{mutexName}\""));

        Thread.Sleep(200);

        mutex.ReleaseMutex();

        Assert.That(Task.WaitAll([p1, p2], 5000));

        var nextValueForP1 = 0;
        var nextValueForP2 = 0;

        for (var i = 0; i < 1000; i++)
        {
            ref var evt = ref _ringBuffer[i];
            if (evt.Key == 101)
            {
                Assert.That(evt.Value, Is.EqualTo(nextValueForP1));
                nextValueForP1++;
            }
            else if (evt.Key == 102)
            {
                Assert.That(evt.Value, Is.EqualTo(nextValueForP2));
                nextValueForP2++;
            }
            else
            {
                Assert.Fail($"Unexpected Key: {evt.Key}");
            }
        }

        Assert.That(nextValueForP1, Is.EqualTo(500));
        Assert.That(nextValueForP2, Is.EqualTo(500));
    }

    [Test]
    public void ShouldGetMinimumGatingSequences()
    {
        var sequence = _ringBuffer.NewSequence();
        _ringBuffer.SetGatingSequences(sequence);

        for (var i = 0; i < 10; i++)
        {
            _ringBuffer.Publish(_ringBuffer.Next());
        }

        sequence.SetValue(4);

        RunRemotePublisher("read-minimum-gating-sequence", $"--ipc-directory-path \"{_ringBuffer.IpcDirectoryPath}\" --expected-value {4}");
    }

    private static void RunRemotePublisher(string command, string commandArguments)
    {
        var process = RemoteIpcPublisher.Start(command, commandArguments);

        Assert.That(process.WaitForExit(50000));
        Assert.That(process.ExitCode, Is.EqualTo(0));
    }
}
#endif
