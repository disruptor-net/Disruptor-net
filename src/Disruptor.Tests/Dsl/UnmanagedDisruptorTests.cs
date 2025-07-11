using System;
using System.Collections.Generic;
using System.Threading;
using Disruptor.Dsl;
using Disruptor.Tests.Dsl.Stubs;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests.Dsl;

[TestFixture]
public class UnmanagedDisruptorTests : IDisposable
{
    private readonly UnmanagedDisruptor<TestValueEvent> _disruptor;
    private readonly StubTaskScheduler _taskScheduler = new();
    private readonly UnmanagedRingBufferMemory _memory;

    public UnmanagedDisruptorTests()
    {
        _memory = UnmanagedRingBufferMemory.Allocate(4, TestValueEvent.Size);
        _disruptor = new UnmanagedDisruptor<TestValueEvent>(_memory.PointerToFirstEvent, _memory.EventSize, _memory.EventCount, _taskScheduler);
    }

    public void Dispose()
    {
        _disruptor.Dispose();
        _memory.Dispose();
        Assert.That(_taskScheduler.JoinAllThreads(1000));
    }

    [Test]
    public void ShouldPublishAndHandleEvent()
    {
        var eventCounter = new CountdownEvent(2);
        var values = new List<int>();

        _disruptor.HandleEventsWith(new TestValueEventHandler<TestValueEvent>(e => values.Add(e.Value)))
                  .Then(new TestValueEventHandler<TestValueEvent>(e => eventCounter.Signal()));

        _disruptor.Start();

        using (var scope = _disruptor.PublishEvent())
        {
            scope.Event().Value = 101;
        }
        using (var scope = _disruptor.PublishEvent())
        {
            scope.Event().Value = 102;
        }

        Assert.That(eventCounter.Wait(TimeSpan.FromSeconds(5)));
        Assert.That(values, Is.EqualTo(new List<int> { 101, 102 }));
    }

    [Test]
    public void ShouldPublishAndHandleEvents()
    {
        var eventCounter = new CountdownEvent(4);
        var values = new List<int>();

        _disruptor.HandleEventsWith(new TestValueEventHandler<TestValueEvent>(e => values.Add(e.Value)))
                  .Then(new TestValueEventHandler<TestValueEvent>(e => eventCounter.Signal()));

        _disruptor.Start();

        using (var scope = _disruptor.PublishEvents(2))
        {
            scope.Event(0).Value = 101;
            scope.Event(1).Value = 102;
        }
        using (var scope = _disruptor.PublishEvents(2))
        {
            scope.Event(0).Value = 103;
            scope.Event(1).Value = 104;
        }

        Assert.That(eventCounter.Wait(TimeSpan.FromSeconds(5)));
        Assert.That(values, Is.EqualTo(new List<int> { 101, 102, 103, 104 }));
    }
}
