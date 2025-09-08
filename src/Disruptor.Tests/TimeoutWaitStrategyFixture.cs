using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Disruptor.Tests;

public abstract class TimeoutWaitStrategyFixture<T> : WaitStrategyFixture<T>
    where T : IWaitStrategy
{
    protected sealed override T CreateWaitStrategy()
    {
        return CreateWaitStrategy(TimeSpan.FromSeconds(30));
    }

    protected abstract T CreateWaitStrategy(TimeSpan timeout);

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
}
