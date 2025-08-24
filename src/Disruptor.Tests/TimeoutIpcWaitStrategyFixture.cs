using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Disruptor.Tests;

public abstract class TimeoutIpcWaitStrategyFixture<T> : IpcWaitStrategyFixture<T>
    where T : IIpcWaitStrategy
{
    protected sealed override T CreateWaitStrategy()
    {
        return CreateWaitStrategy(TimeSpan.FromSeconds(30));
    }

    protected abstract T CreateWaitStrategy(TimeSpan timeout);

    [Test]
    public void ShouldWaitForTimeout()
    {
        var timeout = TimeSpan.FromMilliseconds(500);
        var waitStrategy = CreateWaitStrategy(timeout);
        var waitResult = new TaskCompletionSource<SequenceWaitResult>();

        var sequenceWaiter = waitStrategy.NewSequenceWaiter(SequenceWaiterOwner.Unknown, CreateDependentSequences());
        var stopwatch = new Stopwatch();

        var waitTask = Task.Run(() =>
        {
            stopwatch.Start();
            var sequenceWaitResult = sequenceWaiter.WaitFor(10, CancellationToken);
            stopwatch.Stop();
            waitResult.SetResult(sequenceWaitResult);
        });

        Assert.That(waitTask.Wait(50), Is.False);

        AssertHasResult(waitResult.Task, SequenceWaitResult.Timeout);
        AssertIsCompleted(waitTask);

        // Required to make the test pass on GitHub builds.
        var tolerance = TimeSpan.FromMilliseconds(200);
        Assert.That(stopwatch.Elapsed, Is.GreaterThanOrEqualTo(timeout - tolerance));
    }
}
