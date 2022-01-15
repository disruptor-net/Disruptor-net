using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture(ProducerType.Single)]
[TestFixture(ProducerType.Multi)]
public class SequencerTests
{
    private const int _bufferSize = 16;
    private readonly ProducerType _producerType;
    private ISequencer _sequencer;
    private Sequence _gatingSequence;

    public SequencerTests(ProducerType producerType)
    {
        _producerType = producerType;
        _gatingSequence = new Sequence();
        _sequencer = NewProducer(_producerType, _bufferSize, new BlockingWaitStrategy());
    }

    private ISequencer NewProducer(ProducerType producerType, int bufferSize, IWaitStrategy waitStrategy)
    {
        switch (producerType)
        {
            case ProducerType.Single:
                return new SingleProducerSequencer(bufferSize, waitStrategy);
            case ProducerType.Multi:
                return new MultiProducerSequencer(bufferSize, waitStrategy);
            default:
                throw new ArgumentOutOfRangeException(nameof(producerType), producerType, null);
        }
    }

    [Test]
    public void ShouldStartWithInitialValue()
    {
        Assert.AreEqual(0, _sequencer.Next());
    }

    [Test]
    public void ShouldBatchClaim()
    {
        Assert.AreEqual(3, _sequencer.Next(4));
    }

    [Test]
    public void ShouldIndicateHasAvailableCapacity()
    {
        _sequencer.AddGatingSequences(_gatingSequence);

        Assert.IsTrue(_sequencer.HasAvailableCapacity(1));
        Assert.IsTrue(_sequencer.HasAvailableCapacity(_bufferSize));
        Assert.False(_sequencer.HasAvailableCapacity(_bufferSize + 1));

        _sequencer.Publish(_sequencer.Next());

        Assert.IsTrue(_sequencer.HasAvailableCapacity(_bufferSize - 1));
        Assert.False(_sequencer.HasAvailableCapacity(_bufferSize));
    }

    [Test]
    public void ShouldIndicateNoAvailableCapacity()
    {
        _sequencer.AddGatingSequences(_gatingSequence);

        var sequence = _sequencer.Next(_bufferSize);
        _sequencer.Publish(sequence - (_bufferSize - 1), sequence);

        Assert.IsFalse(_sequencer.HasAvailableCapacity(1));
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

        Assert.IsFalse(_sequencer.TryNext(out _));
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
    public void ShoundNotBeAvailableUntilPublished()
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
        var waitStrategy = new DummyWaitStrategy();
        var sequencer = NewProducer(_producerType, _bufferSize, waitStrategy);

        sequencer.Publish(sequencer.Next());

        Assert.That(waitStrategy.SignalAllWhenBlockingCalls, Is.EqualTo(1));
    }

    [Test]
    public void ShouldNotNotifyNonBlockingWaitStrategyOnPublish()
    {
        var waitStrategy = new DummyWaitStrategy(isBlockingStrategy: false);
        var sequencer = NewProducer(_producerType, _bufferSize, waitStrategy);

        sequencer.Publish(sequencer.Next());

        Assert.That(waitStrategy.SignalAllWhenBlockingCalls, Is.EqualTo(0));
    }

    [Test]
    public void ShouldNotifyWaitStrategyOnPublishBatch()
    {
        var waitStrategy = new DummyWaitStrategy();
        var sequencer = NewProducer(_producerType, _bufferSize, waitStrategy);

        var next = _sequencer.Next(4);
        sequencer.Publish(next - (4 - 1), next);

        Assert.That(waitStrategy.SignalAllWhenBlockingCalls, Is.EqualTo(1));
    }

    [Test]
    public void ShouldNotNotifyNonBlockingWaitStrategyOnPublishBatch()
    {
        var waitStrategy = new DummyWaitStrategy(isBlockingStrategy: false);
        var sequencer = NewProducer(_producerType, _bufferSize, waitStrategy);

        var next = _sequencer.Next(4);
        sequencer.Publish(next - (4 - 1), next);

        Assert.That(waitStrategy.SignalAllWhenBlockingCalls, Is.EqualTo(0));
    }

    [Test]
    public void ShouldWaitOnPublication()
    {
        var barrier = _sequencer.NewBarrier();

        var next = _sequencer.Next(10);
        var lo = next - (10 - 1);
        var mid = next - 5;

        for (var l = lo; l < mid; l++)
        {
            _sequencer.Publish(l);
        }

        Assert.That(barrier.WaitFor(-1), Is.EqualTo(new SequenceWaitResult(mid - 1)));

        for (var l = mid; l <= next; l++)
        {
            _sequencer.Publish(l);
        }
        Assert.That(barrier.WaitFor(-1), Is.EqualTo(new SequenceWaitResult(next)));
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
        Assert.IsFalse(_sequencer.IsAvailable(seq));
        _sequencer.Publish(seq);

        Assert.IsTrue(_sequencer.IsAvailable(seq));
    }

    [Test]
    public void SequencesBecomeUnavailableAfterWrapping()
    {
        var seq = _sequencer.Next();
        _sequencer.Publish(seq);
        Assert.IsTrue(_sequencer.IsAvailable(seq));

        for (var i = 0; i < _bufferSize; i++)
        {
            _sequencer.Publish(_sequencer.Next());
        }

        Assert.IsFalse(_sequencer.IsAvailable(seq));
    }
}