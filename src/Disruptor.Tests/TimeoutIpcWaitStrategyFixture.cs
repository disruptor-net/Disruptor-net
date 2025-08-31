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

        var sequenceWaiter = waitStrategy.NewSequenceWaiter(SequenceWaiterOwner.Unknown, CreateDependentSequences());
        var stopwatch = new Stopwatch();

        Cursor.SetValue(0);

        // Required to make the test pass on GitHub builds.
        var tolerance = TimeSpan.FromMilliseconds(200);

        stopwatch.Start();
        var sequenceWaitResult1 = sequenceWaiter.WaitFor(0, CancellationToken);
        stopwatch.Stop();
        Assert.That(sequenceWaitResult1, Is.EqualTo(new SequenceWaitResult(0)));;
        Assert.That(stopwatch.Elapsed, Is.LessThanOrEqualTo(tolerance));

        stopwatch.Restart();
        var sequenceWaitResult2 = sequenceWaiter.WaitFor(1, CancellationToken);
        stopwatch.Stop();
        Assert.That(sequenceWaitResult2, Is.EqualTo(SequenceWaitResult.Timeout));
        Assert.That(stopwatch.Elapsed, Is.GreaterThanOrEqualTo(timeout - tolerance));
    }
}
