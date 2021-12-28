using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

#if DISRUPTOR_V5

namespace Disruptor.Tests
{
    public class AsyncWaitStrategyTests : WaitStrategyFixture<AsyncWaitStrategy>
    {
        public AsyncWaitStrategyTests()
            : base(new AsyncWaitStrategy(new YieldingWaitStrategy()))
        {
        }

        [Test]
        public void ShouldWaitFromMultipleThreadsAsync()
        {
            // Arrange
            var waitResult1 = new TaskCompletionSource<SequenceWaitResult>();
            var waitResult2 = new TaskCompletionSource<SequenceWaitResult>();

            var dependentSequence1 = Cursor;
            var dependentSequence2 = new Sequence();

            var waitTask1 = Task.Run(async () =>
            {
                waitResult1.SetResult(await WaitStrategy.WaitForAsync(10, Cursor, dependentSequence1, CancellationToken));
                Thread.Sleep(1);
                dependentSequence2.SetValue(10);
            });

            var waitTask2 = Task.Run(async () => waitResult2.SetResult(await WaitStrategy.WaitForAsync(10, Cursor, dependentSequence2, CancellationToken)));

            // Ensure waiting tasks are blocked
            AssertIsNotCompleted(waitResult1.Task);
            AssertIsNotCompleted(waitResult2.Task);

            // Act
            Cursor.SetValue(10);
            WaitStrategy.SignalAllWhenBlocking();

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
            var waitResult1 = new TaskCompletionSource<SequenceWaitResult>();
            var waitResult2 = new TaskCompletionSource<SequenceWaitResult>();
            var waitResult3 = new TaskCompletionSource<SequenceWaitResult>();

            var dependentSequence1 = Cursor;
            var dependentSequence2 = new Sequence();
            var dependentSequence3 = new Sequence();

            var waitTask1 = Task.Run(() =>
            {
                waitResult1.SetResult(WaitStrategy.WaitFor(10, Cursor, dependentSequence1, CancellationToken));
                Thread.Sleep(1);
                dependentSequence2.SetValue(10);
            });

            var waitTask2 = Task.Run(async () =>
            {
                waitResult2.SetResult(await WaitStrategy.WaitForAsync(10, Cursor, dependentSequence2, CancellationToken));
                Thread.Sleep(1);
                dependentSequence3.SetValue(10);
            });

            var waitTask3 = Task.Run(async () => waitResult3.SetResult(await WaitStrategy.WaitForAsync(10, Cursor, dependentSequence3, CancellationToken)));

            // Ensure waiting tasks are blocked
            AssertIsNotCompleted(waitResult1.Task);
            AssertIsNotCompleted(waitResult2.Task);
            AssertIsNotCompleted(waitResult3.Task);

            // Act
            Cursor.SetValue(10);
            WaitStrategy.SignalAllWhenBlocking();

            // Assert WaitFor is unblocked
            AssertHasResult(waitResult1.Task, new SequenceWaitResult(10));
            AssertHasResult(waitResult2.Task, new SequenceWaitResult(10));
            AssertHasResult(waitResult3.Task, new SequenceWaitResult(10));
            AssertIsCompleted(waitTask1);
            AssertIsCompleted(waitTask2);
            AssertIsCompleted(waitTask3);
        }
    }
}

#endif
