using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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
        var waitResult1 = new TaskCompletionSource<SequenceWaitResult>();
        var waitResult2 = new TaskCompletionSource<SequenceWaitResult>();

        var sequence1 = new Sequence();
        var sequenceWaiter1 = waitStrategy.NewSequenceWaiter(SequenceWaiterOwner.Unknown, CreateDependentSequences());
        var stopwatch = Stopwatch.StartNew();

        var waitTask1 = Task.Run(() =>
        {
            waitResult1.SetResult(sequenceWaiter1.WaitFor(10, CancellationToken));
            Thread.Sleep(1);
            sequence1.SetValue(10);
        });

        var sequenceWaiter2 = waitStrategy.NewSequenceWaiter(SequenceWaiterOwner.Unknown, CreateDependentSequences(sequence1));
        var waitTask2 = Task.Run(() => waitResult2.SetResult(sequenceWaiter1.WaitFor(10, CancellationToken)));

        // Ensure waiting tasks are blocked
        AssertIsNotCompleted(waitResult1.Task);
        AssertIsNotCompleted(waitResult2.Task);

        // Act

        // Assert
        AssertHasResult(waitResult1.Task, SequenceWaitResult.Timeout);
        AssertHasResult(waitResult2.Task, SequenceWaitResult.Timeout);
        AssertIsCompleted(waitTask1);
        AssertIsCompleted(waitTask2);

        // Required to make the test pass on azure pipelines.
        var tolerance = TimeSpan.FromMilliseconds(50);

        Assert.That(stopwatch.Elapsed, Is.GreaterThanOrEqualTo(timeout - tolerance));
    }

    [Test]
    public void ShouldWaitFromMultipleThreadsWithTimeoutsAsync()
    {
        // Arrange
        var timeout = TimeSpan.FromMilliseconds(400);
        var waitStrategy = new TimeoutAsyncWaitStrategy(timeout);
        var waitResult1 = new TaskCompletionSource<SequenceWaitResult>();
        var waitResult2 = new TaskCompletionSource<SequenceWaitResult>();

        var sequence1 = new Sequence();
        var sequenceWaiter1 = waitStrategy.NewAsyncSequenceWaiter(SequenceWaiterOwner.Unknown, CreateDependentSequences());
        var stopwatch = Stopwatch.StartNew();

        var waitTask1 = Task.Run(async () =>
        {
            waitResult1.SetResult(await sequenceWaiter1.WaitForAsync(10, CancellationToken));
            Thread.Sleep(1);
            sequence1.SetValue(10);
        });

        var sequenceWaiter2 = waitStrategy.NewAsyncSequenceWaiter(SequenceWaiterOwner.Unknown, CreateDependentSequences(sequence1));
        var waitTask2 = Task.Run(async () => waitResult2.SetResult(await sequenceWaiter2.WaitForAsync(10, CancellationToken)));

        // Ensure waiting tasks are blocked
        AssertIsNotCompleted(waitResult1.Task);
        AssertIsNotCompleted(waitResult2.Task);

        // Act

        // Assert
        AssertHasResult(waitResult1.Task, SequenceWaitResult.Timeout);
        AssertHasResult(waitResult2.Task, SequenceWaitResult.Timeout);
        AssertIsCompleted(waitTask1);
        AssertIsCompleted(waitTask2);

        // Required to make the test pass on azure pipelines.
        var tolerance = TimeSpan.FromMilliseconds(50);

        Assert.That(stopwatch.Elapsed, Is.GreaterThanOrEqualTo(timeout - tolerance));
    }
}
