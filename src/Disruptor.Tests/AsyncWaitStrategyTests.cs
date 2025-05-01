using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Disruptor.Tests;

public abstract class AsyncWaitStrategyTests : WaitStrategyFixture<IAsyncWaitStrategy>
{
    [Test]
    public void ShouldWaitFromMultipleThreadsAsync()
    {
        // Arrange
        var waitStrategy = CreateWaitStrategy();
        var waitResult1 = new TaskCompletionSource<SequenceWaitResult>();
        var waitResult2 = new TaskCompletionSource<SequenceWaitResult>();

        var sequence1 = new Sequence();
        var sequenceWaiter1 = waitStrategy.NewAsyncSequenceWaiter(SequenceWaiterOwner.Unknown, CreateDependentSequences());

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

        var sequence1 = new Sequence();
        var sequenceWaiter1 = waitStrategy.NewSequenceWaiter(SequenceWaiterOwner.Unknown, CreateDependentSequences());

        var waitTask1 = Task.Run(() =>
        {
            waitResult1.SetResult(sequenceWaiter1.WaitFor(10, CancellationToken));
            Thread.Sleep(1);
            sequence1.SetValue(10);
        });

        var sequence2 = new Sequence();
        var sequenceWaiter2 = waitStrategy.NewAsyncSequenceWaiter(SequenceWaiterOwner.Unknown, CreateDependentSequences(sequence1));

        var waitTask2 = Task.Run(async () =>
        {
            waitResult2.SetResult(await sequenceWaiter2.WaitForAsync(10, CancellationToken));
            Thread.Sleep(1);
            sequence2.SetValue(10);
        });

        var sequenceWaiter3 = waitStrategy.NewAsyncSequenceWaiter(SequenceWaiterOwner.Unknown, CreateDependentSequences(sequence2));

        var waitTask3 = Task.Run(async () => waitResult3.SetResult(await sequenceWaiter3.WaitForAsync(10, CancellationToken)));

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
    public void ShouldWaitAfterCancellationAsync()
    {
        // Arrange
        var waitStrategy = CreateWaitStrategy();
        var sequence = new Sequence();
        var sequenceWaiter = waitStrategy.NewAsyncSequenceWaiter(SequenceWaiterOwner.Unknown, CreateDependentSequences(sequence));
        var waitResult = new TaskCompletionSource<Exception>();

        CancellationTokenSource.Cancel();
        sequenceWaiter.Cancel();

        // Act
        var waitTask = Task.Run(async () =>
        {
            try
            {
                await sequenceWaiter.WaitForAsync(10, CancellationToken);
            }
            catch (Exception e)
            {
                waitResult.SetResult(e);
            }
        });

        // Assert
        AssertIsCompleted(waitResult.Task);
        Assert.That(waitResult.Task.Result, Is.InstanceOf<OperationCanceledException>());
        AssertIsCompleted(waitTask);
    }

    [Test]
    public void ShouldUnblockAfterCancellationAsync()
    {
        // Arrange
        var waitStrategy = CreateWaitStrategy();
        var sequence = new Sequence();
        var sequenceWaiter = waitStrategy.NewAsyncSequenceWaiter(SequenceWaiterOwner.Unknown, CreateDependentSequences(sequence));
        var waitResult = new TaskCompletionSource<Exception>();

        var waitTask = Task.Run(async () =>
        {
            try
            {
                await sequenceWaiter.WaitForAsync(10, CancellationToken);
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


    [Test]
    public void ShouldWaitMultipleTimesAsync()
    {
        // Arrange
        var waitStrategy = CreateWaitStrategy();
        var sequence1 = new Sequence();

        var waitTask1 = Task.Run(async () =>
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var sequenceWaiter = waitStrategy.NewAsyncSequenceWaiter(SequenceWaiterOwner.Unknown, CreateDependentSequences());

            for (var i = 0; i < 500; i++)
            {
                await sequenceWaiter.WaitForAsync(i, cancellationTokenSource.Token).ConfigureAwait(false);
                sequence1.SetValue(i);
            }
        });

        var waitTask2 = Task.Run(async () =>
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var sequenceWaiter = waitStrategy.NewAsyncSequenceWaiter(SequenceWaiterOwner.Unknown, CreateDependentSequences(sequence1));

            for (var i = 0; i < 500; i++)
            {
                await sequenceWaiter.WaitForAsync(i, cancellationTokenSource.Token).ConfigureAwait(false);
            }
        });

        // Act
        for (var i = 0; i < 500; i++)
        {
            if (i % 50 == 0)
                Thread.Sleep(1);

            Cursor.SetValue(i);
            waitStrategy.SignalAllWhenBlocking();
        }

        // Assert
        AssertIsCompleted(waitTask1);
        AssertIsCompleted(waitTask2);
    }
}
