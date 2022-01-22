using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Disruptor.Tests;

public abstract class AsyncWaitStrategyTests : WaitStrategyFixture<AsyncWaitStrategy>
{
    [Test]
    public void ShouldWaitFromMultipleThreadsAsync()
    {
        // Arrange
        var waitStrategy = CreateWaitStrategy();
        var waitResult1 = new TaskCompletionSource<SequenceWaitResult>();
        var waitResult2 = new TaskCompletionSource<SequenceWaitResult>();

        var dependentSequence1 = Cursor;
        var dependentSequence2 = new Sequence();

        var waitTask1 = Task.Run(async () =>
        {
            waitResult1.SetResult(await waitStrategy.WaitForAsync(10, Cursor, dependentSequence1, CancellationToken));
            Thread.Sleep(1);
            dependentSequence2.SetValue(10);
        });

        var waitTask2 = Task.Run(async () => waitResult2.SetResult(await waitStrategy.WaitForAsync(10, Cursor, dependentSequence2, CancellationToken)));

        // Ensure waiting tasks are blocked
        AssertIsNotCompleted(waitResult1.Task);
        AssertIsNotCompleted(waitResult2.Task);

        // Act
        Cursor.SetValue(10);
        waitStrategy.SignalAllWhenBlocking();

        // Assert
        AssertHasResult(waitResult1.Task, new SequenceWaitResult(10));
        AssertHasResult(waitResult2.Task, new SequenceWaitResult(10));
        AssertIsCompleted(waitTask1);
        AssertIsCompleted(waitTask2);
    }

    [Test]
    public void ShouldWaitFromMultipleThreadsSyncAndAsync()
    {
        // Arrange
        var waitStrategy = CreateWaitStrategy();
        var waitResult1 = new TaskCompletionSource<SequenceWaitResult>();
        var waitResult2 = new TaskCompletionSource<SequenceWaitResult>();
        var waitResult3 = new TaskCompletionSource<SequenceWaitResult>();

        var dependentSequence1 = Cursor;
        var dependentSequence2 = new Sequence();
        var dependentSequence3 = new Sequence();

        var waitTask1 = Task.Run(() =>
        {
            waitResult1.SetResult(waitStrategy.WaitFor(10, Cursor, dependentSequence1, CancellationToken));
            Thread.Sleep(1);
            dependentSequence2.SetValue(10);
        });

        var waitTask2 = Task.Run(async () =>
        {
            waitResult2.SetResult(await waitStrategy.WaitForAsync(10, Cursor, dependentSequence2, CancellationToken));
            Thread.Sleep(1);
            dependentSequence3.SetValue(10);
        });

        var waitTask3 = Task.Run(async () => waitResult3.SetResult(await waitStrategy.WaitForAsync(10, Cursor, dependentSequence3, CancellationToken)));

        // Ensure waiting tasks are blocked
        AssertIsNotCompleted(waitResult1.Task);
        AssertIsNotCompleted(waitResult2.Task);
        AssertIsNotCompleted(waitResult3.Task);

        // Act
        Cursor.SetValue(10);
        waitStrategy.SignalAllWhenBlocking();

        // Assert WaitFor is unblocked
        AssertHasResult(waitResult1.Task, new SequenceWaitResult(10));
        AssertHasResult(waitResult2.Task, new SequenceWaitResult(10));
        AssertHasResult(waitResult3.Task, new SequenceWaitResult(10));
        AssertIsCompleted(waitTask1);
        AssertIsCompleted(waitTask2);
        AssertIsCompleted(waitTask3);
    }

    [Test]
    public void ShouldUnblockAfterCancellationAsync()
    {
        // Arrange
        var waitStrategy = CreateWaitStrategy();
        var dependentSequence = new Sequence();
        var waitResult = new TaskCompletionSource<Exception>();

        var waitTask = Task.Run(async () =>
        {
            try
            {
                await waitStrategy.WaitForAsync(10, Cursor, dependentSequence, CancellationToken);
            }
            catch (Exception e)
            {
                waitResult.SetResult(e);
            }
        });

        // Ensure waiting tasks are blocked
        AssertIsNotCompleted(waitTask);

        // Act
        CancellationTokenSource.Cancel();
        waitStrategy.SignalAllWhenBlocking();

        // Assert
        AssertIsCompleted(waitResult.Task);
        Assert.That(waitResult.Task.Result, Is.InstanceOf<OperationCanceledException>());
        AssertIsCompleted(waitTask);
    }
}
