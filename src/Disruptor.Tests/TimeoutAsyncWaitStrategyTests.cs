using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests;

public class TimeoutAsyncWaitStrategyTests : AsyncWaitStrategyFixture
{
    protected override IAsyncWaitStrategy CreateWaitStrategy()
    {
        return new TimeoutAsyncWaitStrategy(TimeSpan.FromSeconds(30));
    }

    [Test]
    public void ShouldWaitFromMultipleThreadsWithTimeouts()
    {
        // Arrange
        var timeout = TimeSpan.FromMilliseconds(400);
        var waitStrategy = new TimeoutAsyncWaitStrategy(timeout);

        var sequenceWaiter1 = waitStrategy.NewSequenceWaiter(SequenceWaiterOwner.Unknown, CreateDependentSequences());
        var sequenceWaiter2 = waitStrategy.NewSequenceWaiter(SequenceWaiterOwner.Unknown, CreateDependentSequences());

        var stopwatch1 = new Stopwatch();
        var stopwatch2 = new Stopwatch();

        // Act
        var waitTask1 = Task.Run(() =>
        {
            stopwatch1.Start();
            var waitResult = sequenceWaiter1.WaitFor(10, CancellationToken);
            stopwatch1.Stop();

            return waitResult;
        });

        var waitTask2 = Task.Run(() =>
        {
            stopwatch2.Start();
            var waitResult = sequenceWaiter2.WaitFor(10, CancellationToken);
            stopwatch2.Stop();

            return waitResult;
        });

        // Assert
        AssertHasResult(waitTask1, SequenceWaitResult.Timeout);
        AssertHasResult(waitTask2, SequenceWaitResult.Timeout);
        AssertIsCompleted(waitTask1);
        AssertIsCompleted(waitTask2);

        // Required to make the test pass on azure pipelines.
        var tolerance = TimeSpan.FromMilliseconds(50);
        Assert.That(stopwatch1.Elapsed, Is.GreaterThanOrEqualTo(timeout - tolerance));
        Assert.That(stopwatch2.Elapsed, Is.GreaterThanOrEqualTo(timeout - tolerance));
    }

    [Test]
    public void ShouldWaitFromMultipleThreadsWithTimeoutsAsync()
    {
        // Arrange
        var timeout = TimeSpan.FromMilliseconds(400);
        var waitStrategy = new TimeoutAsyncWaitStrategy(timeout);

        var sequenceWaiter1 = waitStrategy.NewAsyncSequenceWaiter(SequenceWaiterOwner.Unknown, CreateDependentSequences());
        var sequenceWaiter2 = waitStrategy.NewAsyncSequenceWaiter(SequenceWaiterOwner.Unknown, CreateDependentSequences());

        var stopwatch1 = new Stopwatch();
        var stopwatch2 = new Stopwatch();

        // Act
        var waitTask1 = Task.Run(async () =>
        {
            stopwatch1.Start();
            var waitResult = await sequenceWaiter1.WaitForAsync(10, CancellationToken);
            stopwatch1.Stop();
            return waitResult;
        });

        var waitTask2 = Task.Run(async () =>
        {
            stopwatch2.Start();
            var waitResult = await sequenceWaiter2.WaitForAsync(10, CancellationToken);
            stopwatch2.Stop();
            return waitResult;
        });

        // Assert
        AssertHasResult(waitTask1, SequenceWaitResult.Timeout);
        AssertHasResult(waitTask2, SequenceWaitResult.Timeout);
        AssertIsCompleted(waitTask1);
        AssertIsCompleted(waitTask2);

        // Required to make the test pass on azure pipelines.
        var tolerance = TimeSpan.FromMilliseconds(50);
        Assert.That(stopwatch1.Elapsed, Is.GreaterThanOrEqualTo(timeout - tolerance));
        Assert.That(stopwatch2.Elapsed, Is.GreaterThanOrEqualTo(timeout - tolerance));
    }

    [Test]
    public void ShouldStopThreadOnDispose()
    {
        var waitStrategy = new TimeoutAsyncWaitStrategy(TimeSpan.FromSeconds(30));
        Assert.That(!waitStrategy.IsThreadRunning());

        var disruptor = new Disruptor<TestEvent>(() => new TestEvent(), 1024, waitStrategy);
        disruptor.HandleEventsWith(new TestAsyncBatchEventHandler<TestEvent>());

        Assert.That(waitStrategy.IsThreadRunning());

        disruptor.Dispose();

        Assert.That(!waitStrategy.IsThreadRunning());
    }


    [Test]
    public void ShouldRestartThreadOnNewDisruptor()
    {
        var waitStrategy = new TimeoutAsyncWaitStrategy(TimeSpan.FromSeconds(30));

        using var disruptor1 = new Disruptor<TestEvent>(() => new TestEvent(), 1024, waitStrategy);
        disruptor1.HandleEventsWith(new TestAsyncBatchEventHandler<TestEvent>());
        disruptor1.Dispose();

        Assert.That(!waitStrategy.IsThreadRunning());

        using var disruptor2 = new Disruptor<TestEvent>(() => new TestEvent(), 1024, waitStrategy);
        disruptor2.HandleEventsWith(new TestAsyncBatchEventHandler<TestEvent>());

        Assert.That(waitStrategy.IsThreadRunning());
    }
}
