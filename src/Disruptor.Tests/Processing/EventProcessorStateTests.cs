using System.Threading.Tasks;
using Disruptor.Processing;
using NUnit.Framework;

namespace Disruptor.Tests.Processing;

[TestFixture]
public class EventProcessorStateTests
{
    [Test]
    public void ShouldWaitForShutdownBeforeDisposingWaiter()
    {
        var waiter = new TestSequenceBarrier();
        var state = new EventProcessorState(waiter, true);

        var runState = state.Start();
        runState.OnStarted();

        state.Halt();
        state.Dispose();
        Assert.That(waiter.Disposed.Wait(100), Is.False);

        runState.OnShutdown();
        Assert.That(waiter.Disposed.Wait(100), Is.True);
    }

    private class TestSequenceBarrier : ICancellableBarrier
    {
        private readonly TaskCompletionSource _canceled = new();
        private readonly TaskCompletionSource _disposed = new();

        public Task Canceled => _canceled.Task;
        public Task Disposed => _disposed.Task;

        public void Dispose()
        {
            _disposed.SetResult();
        }

        public void CancelProcessing()
        {
            _canceled.SetResult();
        }
    }
}
