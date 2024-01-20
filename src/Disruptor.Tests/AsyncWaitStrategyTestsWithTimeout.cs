using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Disruptor.Tests;

public class AsyncWaitStrategyTestsWithTimeout : AsyncWaitStrategyTests
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
        var stopwatch = Stopwatch.StartNew();

        var waitTask1 = Task.Run(() =>
        {
            waitResult1.SetResult(waitStrategy.WaitFor(10, new DependentSequenceGroup(Cursor), CancellationToken));
            Thread.Sleep(1);
            sequence1.SetValue(10);
        });

        var waitTask2 = Task.Run(() => waitResult2.SetResult(waitStrategy.WaitFor(10, new DependentSequenceGroup(Cursor, sequence1), CancellationToken)));

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
        var stopwatch = Stopwatch.StartNew();

        var waitTask1 = Task.Run(async () =>
        {
            waitResult1.SetResult(await waitStrategy.WaitForAsync(10, new DependentSequenceGroup(Cursor), CancellationToken));
            Thread.Sleep(1);
            sequence1.SetValue(10);
        });

        var waitTask2 = Task.Run(async () => waitResult2.SetResult(await waitStrategy.WaitForAsync(10, new DependentSequenceGroup(Cursor, sequence1), CancellationToken)));

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
