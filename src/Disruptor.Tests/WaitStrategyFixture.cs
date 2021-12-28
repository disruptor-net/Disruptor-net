using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public abstract class WaitStrategyFixture<T>
        where T : IWaitStrategy
    {
        protected WaitStrategyFixture(T waitStrategy)
        {
            WaitStrategy = waitStrategy;
        }

        protected WaitStrategyFixture()
            : this(Activator.CreateInstance<T>())
        {
        }

        protected TimeSpan DefaultAssertTimeout { get; } = TimeSpan.FromSeconds(5);
        protected T WaitStrategy { get; }
        protected Sequence Cursor { get; } = new Sequence();
        protected CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();
        protected CancellationToken CancellationToken => CancellationTokenSource.Token;

        [TestCase(10, 10, 10)]
        [TestCase(12, 10, 10)]
        [TestCase(15, 12, 12)]
        public void ShouldWaitForAvailableSequence(long cursorValue, long dependentSequenceValue, long expectedResult)
        {
            // Arrange
            Cursor.SetValue(cursorValue);

            var dependentSequence = new Sequence(dependentSequenceValue);

            // Act
            var waitResult = WaitStrategy.WaitFor(10, Cursor, dependentSequence, CancellationToken);

            // Assert
            Assert.That(waitResult, Is.EqualTo(new SequenceWaitResult(expectedResult)));
        }

        [TestCase(10, 10, 10)]
        [TestCase(12, 10, 10)]
        [TestCase(15, 12, 12)]
        public void ShouldWaitAndReturnOnceSequenceIsAvailable(long sequence, long dependentSequenceValue, long expectedResult)
        {
            // Arrange
            var dependentSequence = new Sequence();
            var waitResult = new TaskCompletionSource<SequenceWaitResult>();

            var waitTask = Task.Run(() => waitResult.SetResult(WaitStrategy.WaitFor(10, Cursor, dependentSequence, CancellationToken)));

            // Ensure waiting tasks are blocked
            AssertIsNotCompleted(waitTask);

            // Act
            Cursor.SetValue(sequence);
            WaitStrategy.SignalAllWhenBlocking();
            dependentSequence.SetValue(dependentSequenceValue);

            // Assert
            AssertHasResult(waitResult.Task, new SequenceWaitResult(expectedResult));
            AssertIsCompleted(waitTask);
        }

        [Test]
        public void ShouldWaitFromMultipleThreads()
        {
            // Arrange
            var waitResult1 = new TaskCompletionSource<SequenceWaitResult>();
            var waitResult2 = new TaskCompletionSource<SequenceWaitResult>();

            var dependentSequence1 = Cursor;
            var dependentSequence2 = new Sequence();

            var waitTask1 = Task.Run(() =>
            {
                waitResult1.SetResult(WaitStrategy.WaitFor(10, Cursor, dependentSequence1, CancellationToken));
                Thread.Sleep(1);
                dependentSequence2.SetValue(10);
            });

            var waitTask2 = Task.Run(() => waitResult2.SetResult(WaitStrategy.WaitFor(10, Cursor, dependentSequence2, CancellationToken)));

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
        public void ShouldWaitFromMultipleThreadsInOrder()
        {
            // Arrange
            var waitResult1 = new TaskCompletionSource<SequenceWaitResult>();
            var waitResult2 = new TaskCompletionSource<SequenceWaitResult>();
            var task1Signal = new ManualResetEvent(false);

            var dependentSequence1 = Cursor;
            var dependentSequence2 = new Sequence();

            var waitTask1 = Task.Run(() =>
            {
                waitResult1.SetResult(WaitStrategy.WaitFor(10, Cursor, dependentSequence1, CancellationToken));
                task1Signal.WaitOne(DefaultAssertTimeout);
                dependentSequence2.SetValue(10);
            });

            var waitTask2 = Task.Run(() => waitResult2.SetResult(WaitStrategy.WaitFor(10, Cursor, dependentSequence2, CancellationToken)));

            // Ensure waiting tasks are blocked
            AssertIsNotCompleted(waitResult1.Task);
            AssertIsNotCompleted(waitResult2.Task);

            // Act 1: set cursor
            Cursor.SetValue(10);
            WaitStrategy.SignalAllWhenBlocking();

            // Assert
            AssertHasResult(waitResult1.Task, new SequenceWaitResult(10));
            AssertIsNotCompleted(waitResult2.Task);

            // Act 2: unblock task1
            task1Signal.Set();

            // Assert
            AssertHasResult(waitResult2.Task, new SequenceWaitResult(10));
            AssertIsCompleted(waitTask1);
            AssertIsCompleted(waitTask2);
        }

        protected static void AssertIsNotCompleted(Task task)
        {
            Assert.That(task.Wait(2), Is.False);
        }

        protected void AssertIsCompleted(Task task)
        {
            Assert.That(task.Wait(DefaultAssertTimeout), Is.True);
        }

        protected void AssertHasResult<TResult>(Task<TResult> task, TResult expectedValue)
        {
            AssertIsCompleted(task);
            Assert.That(task.Result, Is.EqualTo(expectedValue));
        }
    }
}
