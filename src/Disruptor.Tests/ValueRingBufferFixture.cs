using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.Processing;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture]
public abstract class ValueRingBufferFixture<T> : IDisposable
    where T : struct, IStubEvent
{
    private readonly Func<(int size, ProducerType producerType), IValueRingBuffer<T>> _ringBufferFactory;
    private readonly IValueRingBuffer<T> _ringBuffer;
    private readonly SequenceBarrier _sequenceBarrier;
    private readonly CursorFollower _cursorFollower;

    protected ValueRingBufferFixture(Func<(int size, ProducerType producerType), IValueRingBuffer<T>> ringBufferFactory)
    {
        _ringBufferFactory = ringBufferFactory;
        _ringBuffer = ringBufferFactory.Invoke((32, ProducerType.Multi));
        _sequenceBarrier = _ringBuffer.NewBarrier();
        _cursorFollower = CursorFollower.StartNew(_ringBuffer);
        _ringBuffer.AddGatingSequences(_cursorFollower.Sequence);
    }

    public virtual void Dispose()
    {
        _cursorFollower.Dispose();
    }

    private IValueRingBuffer<T> CreateRingBuffer(int size, ProducerType producerType)
    {
        return _ringBufferFactory.Invoke((size, producerType));
    }

    [Test]
    public void ShouldClaimAndGet()
    {
        Assert.That(_ringBuffer.Cursor, Is.EqualTo(Sequence.InitialCursorValue));

        var expectedEvent = new T { Value = 2701 };

        var claimSequence = _ringBuffer.Next();
        _ringBuffer[claimSequence] = expectedEvent;
        _ringBuffer.Publish(claimSequence);

        var waitResult = _sequenceBarrier.WaitForPublishedSequence(0);
        Assert.That(waitResult, Is.EqualTo(new SequenceWaitResult(0)));

        var evt = _ringBuffer[waitResult.UnsafeAvailableSequence];
        Assert.That(evt, Is.EqualTo(expectedEvent));

        Assert.That(_ringBuffer.Cursor, Is.EqualTo(0L));
    }

    [Test]
    public void ShouldNotClaimMoreThanCapacity()
    {
        Assert.Throws<ArgumentException>(() => _ringBuffer.Next(_ringBuffer.BufferSize + 1));
        Assert.Throws<ArgumentException>(() => _ringBuffer.TryNext(_ringBuffer.BufferSize + 1, out _));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void ShouldNotClaimLessThanOneEvent(int count)
    {
        Assert.Throws<ArgumentException>(() => _ringBuffer.Next(count));
        Assert.Throws<ArgumentException>(() => _ringBuffer.TryNext(count, out _));
    }

    [Test]
    public void ShouldClaimAndGetInSeparateThread()
    {
        var events = GetEvents(0, 0);

        var expectedEvent = new T { Value = 2701 };

        var sequence = _ringBuffer.Next();
        _ringBuffer[sequence] = expectedEvent;
        _ringBuffer.Publish(sequence);

        Assert.That(events.Result[0], Is.EqualTo(expectedEvent));
    }

    [Test]
    public void ShouldClaimAndGetMultipleMessages()
    {
        var numEvents = _ringBuffer.BufferSize;
        for (var i = 0; i < numEvents; i++)
        {
            var sequence = _ringBuffer.Next();
            _ringBuffer[sequence].Value = i;
            _ringBuffer.Publish(sequence);
        }

        var expectedSequence = numEvents - 1;
        var waitResult = _sequenceBarrier.WaitForPublishedSequence(expectedSequence);
        Assert.That(waitResult, Is.EqualTo(new SequenceWaitResult(expectedSequence)));

        for (var i = 0; i < numEvents; i++)
        {
            Assert.That(_ringBuffer[i].Value, Is.EqualTo(i));
        }
    }

    [Test]
    public void ShouldWrap()
    {
        var numEvents = _ringBuffer.BufferSize;
        const int offset = 1000;
        for (var i = 0; i < numEvents + offset; i++)
        {
            var sequence = _ringBuffer.Next();
            _ringBuffer[sequence].Value = i;
            _ringBuffer.Publish(sequence);
        }

        var expectedSequence = numEvents + offset - 1;
        var waitResult = _sequenceBarrier.WaitForPublishedSequence(expectedSequence);
        Assert.That(waitResult, Is.EqualTo(new SequenceWaitResult(expectedSequence)));

        for (var i = offset; i < numEvents + offset; i++)
        {
            Assert.That(_ringBuffer[i].Value, Is.EqualTo(i));
        }
    }

    private Task<List<T>> GetEvents(long initial, long toWaitFor)
    {
        var barrier = new Barrier(2);
        var dependencyBarrier = _ringBuffer.NewBarrier();

        var testWaiter = new TestWaiter(barrier, dependencyBarrier, _ringBuffer, initial, toWaitFor);
        var task = Task.Factory.StartNew(() => testWaiter.Call());

        barrier.SignalAndWait();

        return task;
    }

    [Test]
    public void ShouldPreventWrapping()
    {
        var sequence = new Sequence();
        var ringBuffer = CreateRingBuffer(4, ProducerType.Multi);
        ringBuffer.AddGatingSequences(sequence);

        for (var i = 0; i <= 3; i++)
        {
            var l = ringBuffer.Next();
            ringBuffer.Publish(l);
        }

        Assert.That(!ringBuffer.TryNext(out _));
    }

    [Test]
    public void ShouldReturnFalseIfBufferIsFull()
    {
        _ringBuffer.AddGatingSequences(new Sequence(_ringBuffer.BufferSize));

        for (var i = 0; i < _ringBuffer.BufferSize; i++)
        {
            var succeeded = _ringBuffer.TryNext(out var n);
            Assert.That(succeeded);

            _ringBuffer.Publish(n);
        }

        Assert.That(!_ringBuffer.TryNext(out _));
    }

    [Test]
    public void ShouldPreventProducersOvertakingEventProcessorsWrapPoint()
    {
        const int ringBufferSize = 4;
        var mre = new ManualResetEvent(false);
        var ringBuffer = CreateRingBuffer(ringBufferSize, ProducerType.Multi);
        var processor = new TestEventProcessor(ringBuffer.NewBarrier());
        ringBuffer.AddGatingSequences(processor.Sequence);

        var task = Task.Run(() =>
        {
            // Attempt to put in enough events to wrap around the ring buffer
            for (var i = 0; i < ringBufferSize + 1; i++)
            {
                var sequence = ringBuffer.Next();
                ref var evt = ref ringBuffer[sequence];
                evt.Value = i;
                ringBuffer.Publish(sequence);

                if (i == 3) // unblock main thread after 4th eventData published
                {
                    mre.Set();
                }
            }
        });

        mre.WaitOne();

        // Publisher should not be complete, blocked at RingBuffer.Next
        Assert.That(!task.IsCompleted);

        // Run the processor, freeing up entries in the ring buffer for the producer to continue and "complete"
        processor.Start();

        // Check producer completes
        Assert.That(task.Wait(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public void ShouldAddAndRemoveSequences()
    {
        var ringBuffer = CreateRingBuffer(16, ProducerType.Single);

        var sequenceThree = new Sequence(-1);
        var sequenceSeven = new Sequence(-1);
        ringBuffer.AddGatingSequences(sequenceThree, sequenceSeven);

        for (var i = 0; i < 10; i++)
        {
            ringBuffer.Publish(ringBuffer.Next());
        }

        sequenceThree.SetValue(3);
        sequenceSeven.SetValue(7);

        Assert.That(ringBuffer.GetMinimumGatingSequence(), Is.EqualTo(3L));
        Assert.That(ringBuffer.RemoveGatingSequence(sequenceThree));
        Assert.That(ringBuffer.GetMinimumGatingSequence(), Is.EqualTo(7L));
    }

    protected static void AssertEmptyRingBuffer(IValueRingBuffer<long> ringBuffer)
    {
        Assert.That(ringBuffer[0], Is.EqualTo(-1));
        Assert.That(ringBuffer[1], Is.EqualTo(-1));
        Assert.That(ringBuffer[2], Is.EqualTo(-1));
        Assert.That(ringBuffer[3], Is.EqualTo(-1));
    }

    private class TestWaiter
    {
        private readonly Barrier _barrier;
        private readonly SequenceBarrier _sequenceBarrier;
        private readonly long _initialSequence;
        private readonly long _toWaitForSequence;
        private readonly IValueRingBuffer<T> _ringBuffer;

        public TestWaiter(Barrier barrier, SequenceBarrier sequenceBarrier, IValueRingBuffer<T> ringBuffer, long initialSequence, long toWaitForSequence)
        {
            _barrier = barrier;
            _sequenceBarrier = sequenceBarrier;
            _ringBuffer = ringBuffer;
            _initialSequence = initialSequence;
            _toWaitForSequence = toWaitForSequence;
        }

        public List<T> Call()
        {
            _barrier.SignalAndWait();
            _sequenceBarrier.WaitForPublishedSequence(_toWaitForSequence);

            var events = new List<T>();
            for (var l = _initialSequence; l <= _toWaitForSequence; l++)
            {
                events.Add(_ringBuffer[l]);
            }

            return events;
        }
    }
}
