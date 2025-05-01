using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture]
public abstract class SequencerTests
{
    private const int _bufferSize = 16;
    protected readonly ISequencer _sequencer;
    protected readonly Sequence _gatingSequence;

    public SequencerTests()
    {
        _gatingSequence = new Sequence();
        _sequencer = NewSequencer(new BlockingWaitStrategy());
    }

    protected abstract ISequencer NewSequencer(IWaitStrategy waitStrategy, int bufferSize = 16);

    [Test]
    public void ShouldStartWithInitialValue()
    {
        Assert.That(_sequencer.Next(), Is.EqualTo((long)0));
    }

    [Test]
    public void ShouldBatchClaim()
    {
        Assert.That(_sequencer.Next(4), Is.EqualTo((long)3));
    }

    [Test]
    public void ShouldIndicateHasAvailableCapacity()
    {
        _sequencer.AddGatingSequences(_gatingSequence);

        Assert.That(_sequencer.HasAvailableCapacity(1));
        Assert.That(_sequencer.HasAvailableCapacity(_bufferSize));
        Assert.That(!_sequencer.HasAvailableCapacity(_bufferSize + 1));

        _sequencer.Publish(_sequencer.Next());

        Assert.That(_sequencer.HasAvailableCapacity(_bufferSize - 1));
        Assert.That(!_sequencer.HasAvailableCapacity(_bufferSize));
    }

    [Test]
    public void ShouldIndicateNoAvailableCapacity()
    {
        _sequencer.AddGatingSequences(_gatingSequence);

        var sequence = _sequencer.Next(_bufferSize);
        _sequencer.Publish(sequence - (_bufferSize - 1), sequence);

        Assert.That(!_sequencer.HasAvailableCapacity(1));
    }

    [Test]
    public void ShouldHoldUpPublisherWhenBufferIsFull()
    {
        _sequencer.AddGatingSequences(_gatingSequence);
        var sequence = _sequencer.Next(_bufferSize);
        _sequencer.Publish(sequence - (_bufferSize - 1), sequence);

        var waitingSignal = new ManualResetEvent(false);
        var doneSignal = new ManualResetEvent(false);

        var expectedFullSequence = Sequence.InitialCursorValue + _sequencer.BufferSize;
        Assert.That(_sequencer.Cursor, Is.EqualTo(expectedFullSequence));

        Task.Run(() =>
        {
            waitingSignal.Set();

            var next = _sequencer.Next();
            _sequencer.Publish(next);

            doneSignal.Set();
        });

        waitingSignal.WaitOne(TimeSpan.FromMilliseconds(500));
        Assert.That(_sequencer.ComputeHighestPublishedSequence(), Is.EqualTo(expectedFullSequence));

        _gatingSequence.SetValue(Sequence.InitialCursorValue + 1L);

        doneSignal.WaitOne(TimeSpan.FromMilliseconds(500));
        Assert.That(_sequencer.Cursor, Is.EqualTo(expectedFullSequence + 1L));
    }

    [Test]
    public void ShouldReturnFalseWhenSequencerIsFull()
    {
        _sequencer.AddGatingSequences(_gatingSequence);

        for (var i = 0; i < _bufferSize; i++)
        {
            _sequencer.Next();
        }

        Assert.That(!_sequencer.TryNext(out _));
    }

    [Test]
    public void ShouldCalculateRemainingCapacity()
    {
        _sequencer.AddGatingSequences(_gatingSequence);

        Assert.That(_sequencer.GetRemainingCapacity(), Is.EqualTo(_bufferSize));

        for (var i = 1; i < _bufferSize; i++)
        {
            _sequencer.Next();
            Assert.That(_sequencer.GetRemainingCapacity(), Is.EqualTo(_bufferSize - i));
        }
    }

    [Test]
    public void ShouldNotBeAvailableUntilPublished()
    {
        var next = _sequencer.Next(6);

        for (var i = 0; i <= 5; i++)
        {
            Assert.That(_sequencer.IsAvailable(i), Is.False);
        }

        _sequencer.Publish(next - (6 - 1), next);

        for (var i = 0; i <= 5; i++)
        {
            Assert.That(_sequencer.IsAvailable(i), Is.True);
        }

        Assert.That(_sequencer.IsAvailable(6), Is.False);
    }

    [Test]
    public void ShouldNotifyWaitStrategyOnPublish()
    {
        var waitStrategy = new DummyWaitStrategy { IsBlockingStrategy = true };
        var sequencer = NewSequencer(waitStrategy);

        sequencer.Publish(sequencer.Next());

        Assert.That(waitStrategy.SignalAllWhenBlockingCalls, Is.EqualTo(1));
    }

    [Test]
    public void ShouldNotNotifyNonBlockingWaitStrategyOnPublish()
    {
        var waitStrategy = new DummyWaitStrategy { IsBlockingStrategy = false };
        var sequencer = NewSequencer(waitStrategy);

        sequencer.Publish(sequencer.Next());

        Assert.That(waitStrategy.SignalAllWhenBlockingCalls, Is.EqualTo(0));
    }

    [Test]
    public void ShouldNotifyWaitStrategyOnPublishBatch()
    {
        var waitStrategy = new DummyWaitStrategy { IsBlockingStrategy = true };
        var sequencer = NewSequencer(waitStrategy);

        var next = _sequencer.Next(4);
        sequencer.Publish(next - (4 - 1), next);

        Assert.That(waitStrategy.SignalAllWhenBlockingCalls, Is.EqualTo(1));
    }

    [Test]
    public void ShouldNotNotifyNonBlockingWaitStrategyOnPublishBatch()
    {
        var waitStrategy = new DummyWaitStrategy { IsBlockingStrategy = false };
        var sequencer = NewSequencer(waitStrategy);

        var next = _sequencer.Next(4);
        sequencer.Publish(next - (4 - 1), next);

        Assert.That(waitStrategy.SignalAllWhenBlockingCalls, Is.EqualTo(0));
    }

    [Test]
    public void ShouldWaitOnPublication()
    {
        var barrier = _sequencer.NewBarrier(SequenceWaiterOwner.Unknown);

        var next = _sequencer.Next(10);
        var lo = next - (10 - 1);
        var mid = next - 5;

        for (var l = lo; l < mid; l++)
        {
            _sequencer.Publish(l);
        }

        Assert.That(barrier.WaitForPublishedSequence(-1), Is.EqualTo(new SequenceWaitResult(mid - 1)));

        for (var l = mid; l <= next; l++)
        {
            _sequencer.Publish(l);
        }
        Assert.That(barrier.WaitForPublishedSequence(-1), Is.EqualTo(new SequenceWaitResult(next)));
    }

    [Test]
    public async Task ShouldWaitOnPublicationAsync()
    {
        var sequencer = NewSequencer(new AsyncWaitStrategy());
        var barrier = sequencer.NewAsyncBarrier(SequenceWaiterOwner.Unknown);

        var next = sequencer.Next(10);
        var lo = next - (10 - 1);
        var mid = next - 5;

        for (var l = lo; l < mid; l++)
        {
            sequencer.Publish(l);
        }

        var s1 = await barrier.WaitForPublishedSequenceAsync(-1);

        Assert.That(s1, Is.EqualTo(new SequenceWaitResult(mid - 1)));

        for (var l = mid; l <= next; l++)
        {
            sequencer.Publish(l);
        }

        var s2 = await barrier.WaitForPublishedSequenceAsync(-1);

        Assert.That(s2, Is.EqualTo(new SequenceWaitResult(next)));
    }

    [Test]
    public void ShouldCreateBarrierUsingSpecifiedEventHandler()
    {
        var waitStrategy = new TestWaitStrategy();
        var sequencer = NewSequencer(waitStrategy);
        var eventHandler = new TestEventHandler<TestEvent>();

        waitStrategy.SetupNextSequence(eventHandler, new SequenceWaitResult(42));

        var barrier = sequencer.NewBarrier(SequenceWaiterOwner.EventHandler(eventHandler));
        var waitResult = barrier.WaitFor(0);

        Assert.That(waitResult, Is.EqualTo(new SequenceWaitResult(42)));
    }

    [Test]
    public async Task ShouldCreateBarrierUsingSpecifiedEventHandlerAsync()
    {
        var waitStrategy = new TestWaitStrategy();
        var sequencer = NewSequencer(waitStrategy);
        var eventHandler = new TestEventHandler<TestEvent>();

        waitStrategy.SetupNextSequence(eventHandler, new SequenceWaitResult(42));

        var barrier = sequencer.NewAsyncBarrier(SequenceWaiterOwner.EventHandler(eventHandler));
        var waitResult = await barrier.WaitForAsync(0);

        Assert.That(waitResult, Is.EqualTo(new SequenceWaitResult(42)));
    }

    [Test]
    public void ShouldTryNext()
    {
        _sequencer.AddGatingSequences(_gatingSequence);

        for (int i = 0; i < _bufferSize; i++)
        {
            var succeeded = _sequencer.TryNext(out var sequence);
            Assert.That(succeeded, Is.True);
            Assert.That(sequence, Is.EqualTo(i));

            _sequencer.Publish(i);
        }

        Assert.That(_sequencer.TryNext(out var _), Is.False);
    }

    [Test]
    public void ShouldTryNextN()
    {
        _sequencer.AddGatingSequences(_gatingSequence);

        for (int i = 0; i < _bufferSize; i += 2)
        {
            var succeeded = _sequencer.TryNext(2, out var sequence);
            Assert.That(succeeded, Is.True);
            Assert.That(sequence, Is.EqualTo(i + 1));

            _sequencer.Publish(i);
            _sequencer.Publish(i + 1);
        }

        Assert.That(_sequencer.TryNext(1, out var _), Is.False);
    }

    [Test]
    public void ShouldClaimSpecificSequence()
    {
        long sequence = 14L;

        _sequencer.Claim(sequence);
        _sequencer.Publish(sequence);
        Assert.That(_sequencer.Next(), Is.EqualTo(sequence + 1));
    }

    [Test]
    public void ShouldNotAllowBulkNextLessThanZero()
    {
        Assert.Throws<ArgumentException>(() => _sequencer.Next(-1));
    }

    [Test]
    public void ShouldNotAllowBulkNextOfZero()
    {
        Assert.Throws<ArgumentException>(() => _sequencer.Next(0));
    }

    [Test]
    public void ShouldAllowBulkNextEqualToBufferSize()
    {
        Assert.DoesNotThrow(() => _sequencer.Next(_bufferSize));
    }

    [Test]
    public void ShouldNotAllowBulkNextGreaterThanRingBufferSize()
    {
        Assert.Throws<ArgumentException>(() => _sequencer.Next(_bufferSize + 1));
    }

    [Test]
    public void ShouldNotAllowBulkTryNextLessThanZero()
    {
        Assert.Throws<ArgumentException>(() => _sequencer.TryNext(-1, out _));
    }

    [Test]
    public void ShouldNotAllowBulkTryNextOfZero()
    {
        Assert.Throws<ArgumentException>(() => _sequencer.TryNext(0, out _));
    }

    [Test]
    public void ShouldAllowBulkTryNextEqualToBufferSize()
    {
        Assert.DoesNotThrow(() => _sequencer.TryNext(_bufferSize, out _));
    }

    [Test]
    public void ShouldNotAllowBulkTryNextGreaterThanRingBufferSize()
    {
        Assert.Throws<ArgumentException>(() => _sequencer.TryNext(_bufferSize + 1, out _));
    }

    [Test]
    public void SequencesBecomeAvailableAfterAPublish()
    {
        var seq = _sequencer.Next();
        Assert.That(!_sequencer.IsAvailable(seq));
        _sequencer.Publish(seq);

        Assert.That(_sequencer.IsAvailable(seq));
    }

    [Test]
    public void SequencesBecomeUnavailableAfterWrapping()
    {
        var seq = _sequencer.Next();
        _sequencer.Publish(seq);
        Assert.That(_sequencer.IsAvailable(seq));

        for (var i = 0; i < _bufferSize; i++)
        {
            _sequencer.Publish(_sequencer.Next());
        }

        Assert.That(!_sequencer.IsAvailable(seq));
    }
}
