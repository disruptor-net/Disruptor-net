using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Disruptor.Dsl;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests.Dsl;

[TestFixture]
public class DisposingTests
{
    [Test]
    public void ShouldDisposeEventProcessorSequenceBarrierOnDispose()
    {
        var waitStrategy = new TrackingWaitStrategy();
        using var disruptor = new Disruptor<TestEvent>(() => new TestEvent(), 1024, waitStrategy);

        disruptor.HandleEventsWith(new TestEventHandler<TestEvent>());
        disruptor.HandleEventsWith(new TestBatchEventHandler<TestEvent>());
        Assert.That(waitStrategy.GetWaiters(), Has.Count.EqualTo(2));

        disruptor.Dispose();
        Assert.That(waitStrategy.GetWaiters(), Has.Count.EqualTo(0));
    }

    [Test]
    public void ShouldNotDisposeExternalEventProcessorSequenceBarrierOnDispose()
    {
        var waitStrategy = new TrackingWaitStrategy();
        using var disruptor = new Disruptor<TestEvent>(() => new TestEvent(), 1024, waitStrategy);
        var externalEventProcessor = new TestEventProcessor(disruptor.RingBuffer.NewBarrier());

        disruptor.HandleEventsWith(new TestEventHandler<TestEvent>());
        disruptor.HandleEventsWith(externalEventProcessor);
        Assert.That(waitStrategy.GetWaiters(), Has.Count.EqualTo(2));

        disruptor.Dispose();
        var waiters = waitStrategy.GetWaiters();
        Assert.That(waiters, Has.Count.EqualTo(1));
        Assert.That(waiters[0].Handler, Is.Null);

        externalEventProcessor.Dispose();
        Assert.That(waitStrategy.GetWaiters(), Has.Count.EqualTo(0));
    }

    [Test]
    public void ShouldDisposeEventProcessorSequenceBarrierOnDispose_Value()
    {
        var waitStrategy = new TrackingWaitStrategy();
        using var disruptor = new ValueDisruptor<TestValueEvent>(() => new TestValueEvent(), 1024, waitStrategy);

        disruptor.HandleEventsWith(new TestValueEventHandler<TestValueEvent>());
        Assert.That(waitStrategy.GetWaiters(), Has.Count.EqualTo(1));

        disruptor.Dispose();
        Assert.That(waitStrategy.GetWaiters(), Has.Count.EqualTo(0));
    }

    [Test]
    public void ShouldNotDisposeExternalEventProcessorSequenceBarrierOnDispose_Value()
    {
        var waitStrategy = new TrackingWaitStrategy();
        using var disruptor = new ValueDisruptor<TestValueEvent>(() => new TestValueEvent(), 1024, waitStrategy);
        var externalEventProcessor = new TestEventProcessor(disruptor.RingBuffer.NewBarrier());

        disruptor.HandleEventsWith(new TestValueEventHandler<TestValueEvent>());
        disruptor.HandleEventsWith(externalEventProcessor);
        Assert.That(waitStrategy.GetWaiters(), Has.Count.EqualTo(2));

        disruptor.Dispose();
        var waiters = waitStrategy.GetWaiters();
        Assert.That(waiters, Has.Count.EqualTo(1));
        Assert.That(waiters[0].Handler, Is.Null);

        externalEventProcessor.Dispose();
        Assert.That(waitStrategy.GetWaiters(), Has.Count.EqualTo(0));
    }

    private class TrackingWaitStrategy : IWaitStrategy
    {
        private readonly BlockingWaitStrategy _innerStrategy = new();
        private readonly List<SequenceWaiterOwner> _waiters = new();

        public bool IsBlockingStrategy => _innerStrategy.IsBlockingStrategy;

        public List<SequenceWaiterOwner> GetWaiters()
        {
            lock (_waiters)
            {
                return _waiters.ToList();
            }
        }

        public ISequenceWaiter NewSequenceWaiter(SequenceWaiterOwner owner, DependentSequenceGroup dependentSequences)
        {
            lock (_waiters)
            {
                _waiters.Add(owner);
            }

            return new TrackingSequenceWaiter(_innerStrategy.NewSequenceWaiter(owner, dependentSequences), this, owner);
        }

        public void SignalAllWhenBlocking()
        {
            _innerStrategy.SignalAllWhenBlocking();
        }

        private void OnWaiterDisposed(SequenceWaiterOwner owner)
        {
            lock (_waiters)
            {
                _waiters.Remove(owner);
            }
        }

        private class TrackingSequenceWaiter(ISequenceWaiter innerWaiter, TrackingWaitStrategy waitStrategy, SequenceWaiterOwner owner) : ISequenceWaiter
        {
            public void Dispose()
            {
                waitStrategy.OnWaiterDisposed(owner);
            }

            public SequenceWaitResult WaitFor(long sequence, CancellationToken cancellationToken)
            {
                return innerWaiter.WaitFor(sequence, cancellationToken);
            }

            public void Cancel()
            {
                innerWaiter.Cancel();
            }
        }
    }
}
