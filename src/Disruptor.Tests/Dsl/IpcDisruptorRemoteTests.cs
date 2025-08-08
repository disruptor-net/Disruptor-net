#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests.Dsl;

/// <summary>
/// IpcDisruptor tests using a remote (inter-process) IPC publisher.
/// </summary>
[TestFixture]
public class IpcDisruptorRemoteTests : IAsyncDisposable
{
    private readonly IpcDisruptor<StubUnmanagedEvent> _disruptor;

    public IpcDisruptorRemoteTests()
    {
        _disruptor = new IpcDisruptor<StubUnmanagedEvent>(1024, new YieldingWaitStrategy());
    }

    public async ValueTask DisposeAsync()
    {
        await _disruptor.DisposeAsync();
    }

    [Test]
    public void ShouldPublishAndHandleEvent()
    {
        var eventCounter = new CountdownEvent(2);
        var values = new List<StubUnmanagedEvent>();

        _disruptor.HandleEventsWith(new TestValueEventHandler<StubUnmanagedEvent>(e => values.Add(e)))
                  .Then(new TestValueEventHandler<StubUnmanagedEvent>(e => eventCounter.Signal()));

        _disruptor.Start();

        RemoteIpcPublisher.Run("publish-events", $"--ipc-directory-path \"{_disruptor.IpcDirectoryPath}\"");

        Assert.That(eventCounter.Wait(TimeSpan.FromSeconds(5)));
        Assert.That(values, Is.EqualTo(new List<StubUnmanagedEvent> { new(0, 101), new(1, 102) }));
    }

    [Test]
    public void ShouldPublishAndHandleEvents()
    {
        var eventCounter = new CountdownEvent(4);
        var values = new List<StubUnmanagedEvent>();

        _disruptor.HandleEventsWith(new TestValueEventHandler<StubUnmanagedEvent>(e => values.Add(e)))
                  .Then(new TestValueEventHandler<StubUnmanagedEvent>(e => eventCounter.Signal()));

        _disruptor.Start();

        RemoteIpcPublisher.Run("publish-events-batch", $"--ipc-directory-path \"{_disruptor.IpcDirectoryPath}\"");

        Assert.That(eventCounter.Wait(TimeSpan.FromSeconds(5)));
        Assert.That(values, Is.EqualTo(new List<StubUnmanagedEvent> { new(0, 101), new(1, 102), new(2, 103), new(3, 104) }));
    }

    [Test]
    public void ShouldProcessMessagesPublishedBeforeStartIsCalled()
    {
        var eventCounter = new CountdownEvent(2);
        _disruptor.HandleEventsWith(new TestValueEventHandler<StubUnmanagedEvent>(e => eventCounter.Signal()));

        RemoteIpcPublisher.Run("publish-many-events", $"--ipc-directory-path \"{_disruptor.IpcDirectoryPath}\" --event-count 1");

        _disruptor.Start();

        RemoteIpcPublisher.Run("publish-many-events", $"--ipc-directory-path \"{_disruptor.IpcDirectoryPath}\" --event-count 1");

        if (!eventCounter.Wait(TimeSpan.FromSeconds(5)))
            Assert.Fail("Did not process event published before start was called. Missed events: " + eventCounter.CurrentCount);
    }
}
#endif
