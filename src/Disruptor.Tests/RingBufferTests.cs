using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture]
public class RingBufferTests : IDisposable
{
    private readonly RingBuffer<StubEvent> _ringBuffer;
    private readonly SequenceBarrier _sequenceBarrier;
    private readonly CursorFollower _cursorFollower;

    public RingBufferTests()
    {
        _ringBuffer = RingBuffer<StubEvent>.CreateMultiProducer(() => new StubEvent(-1), 32);
        _sequenceBarrier = _ringBuffer.NewBarrier();
        _cursorFollower = CursorFollower.StartNew(_ringBuffer);
        _ringBuffer.AddGatingSequences(_cursorFollower.Sequence);
    }

    public void Dispose()
    {
        _cursorFollower.Dispose();
    }

    [Test]
    public void ShouldClaimAndGet()
    {
        Assert.That(_ringBuffer.Cursor, Is.EqualTo(Sequence.InitialCursorValue));

        var expectedEvent = new StubEvent(2701);

        var claimSequence = _ringBuffer.Next();
        var oldEvent = _ringBuffer[claimSequence];
        oldEvent.Copy(expectedEvent);
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

    [Test]
    public void ShouldClaimAndGetInSeparateThread()
    {
        var events = GetEvents(0, 0);

        var expectedEvent = new StubEvent(2701);

        using (var scope = _ringBuffer.PublishEvent())
        {
            scope.Event().Copy(expectedEvent);
        }

        Assert.That(events.Result[0], Is.EqualTo(expectedEvent));
    }

    [Test]
    public void ShouldClaimAndGetMultipleMessages()
    {
        var numEvents = _ringBuffer.BufferSize;
        for (var i = 0; i < numEvents; i++)
        {
            using (var scope = _ringBuffer.PublishEvent())
            {
                scope.Event().Value = i;
            }
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
            using (var scope = _ringBuffer.PublishEvent())
            {
                scope.Event().Value = i;
            }
        }

        var expectedSequence = numEvents + offset - 1;
        var waitResult = _sequenceBarrier.WaitForPublishedSequence(expectedSequence);
        Assert.That(waitResult, Is.EqualTo(new SequenceWaitResult(expectedSequence)));

        for (var i = offset; i < numEvents + offset; i++)
        {
            Assert.That(_ringBuffer[i].Value, Is.EqualTo(i));
        }
    }

    private Task<List<StubEvent>> GetEvents(long initial, long toWaitFor)
    {
        var barrier = new Barrier(2);
        var dependencyBarrier = _ringBuffer.NewBarrier();

        var testWaiter = new TestWaiter(barrier, dependencyBarrier, _ringBuffer, initial, toWaitFor);
        var task = Task.Run(() => testWaiter.Call());

        barrier.SignalAndWait();

        return task;
    }

    [Test]
    public void ShouldPreventWrapping()
    {
        var sequence = new Sequence();
        var ringBuffer = RingBuffer<StubEvent>.CreateMultiProducer(() => new StubEvent(-1), 4);
        ringBuffer.AddGatingSequences(sequence);

        for (var i = 0; i <= 3; i++)
        {
            ringBuffer.PublishEvent().Dispose();
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
        var ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(-1), ringBufferSize);
        var processor = new TestEventProcessor(ringBuffer.NewBarrier());
        ringBuffer.AddGatingSequences(processor.Sequence);

        var task = Task.Run(() =>
        {
            // Attempt to put in enough events to wrap around the ring buffer
            for (var i = 0; i < ringBufferSize + 1; i++)
            {
                var sequence = ringBuffer.Next();
                var evt = ringBuffer[sequence];
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
        processor.Run();

        // Check producer completes
        Assert.That(task.Wait(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public void ShouldPublishEvent()
    {
        var ringBuffer = RingBuffer<LongEvent>.CreateSingleProducer(() => new LongEvent(-1), 4);

        using (var scope = ringBuffer.PublishEvent())
        {
            scope.Event().Value = scope.Sequence;
        }
        using (var scope = ringBuffer.TryPublishEvent())
        {
            Assert.That(scope.HasEvent);
            Assert.That(scope.TryGetEvent(out var e));
            e.Event().Value = e.Sequence;
        }

        Assert.That(ringBuffer, IsRingBuffer.WithEvents(new LongEvent(0), new LongEvent(1)));
    }

    [Test]
    public void ShouldPublishEvents()
    {
        var ringBuffer = RingBuffer<LongEvent>.CreateSingleProducer(() => new LongEvent(-1), 4);

        using (var scope = ringBuffer.PublishEvents(2))
        {
            scope.Event(0).Value = scope.StartSequence;
            scope.Event(1).Value = scope.StartSequence + 1;
        }
        Assert.That(ringBuffer, IsRingBuffer.WithEvents(new LongEvent(0), new LongEvent(1), new LongEvent(-1), new LongEvent(-1)));

        using (var scope = ringBuffer.TryPublishEvents(2))
        {
            Assert.That(scope.HasEvents);
            Assert.That(scope.TryGetEvents(out var e));
            e.Event(0).Value = e.StartSequence;
            e.Event(1).Value = e.StartSequence + 1;
        }

        Assert.That(ringBuffer, IsRingBuffer.WithEvents(new LongEvent(0), new LongEvent(1), new LongEvent(2), new LongEvent(3)));
    }

    [Test]
    public void ShouldNotPublishEventsIfBatchIsLargerThanRingBuffer()
    {
        var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);

        try
        {
            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(5));
        }
        finally
        {
            AssertEmptyRingBuffer(ringBuffer);
        }
    }

    [Test]
    public void ShouldNotPublishEventsWhenBatchSizeIs0()
    {
        var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);

        try
        {
            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(0));
        }
        finally
        {
            AssertEmptyRingBuffer(ringBuffer);
        }
    }

    [Test]
    public void ShouldNotTryPublishEventsWhenBatchSizeIs0()
    {
        var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);

        try
        {
            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(0));
        }
        finally
        {
            AssertEmptyRingBuffer(ringBuffer);
        }
    }

    [Test]
    public void ShouldNotPublishEventsWhenBatchSizeIsNegative()
    {
        var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);

        try
        {
            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(-1));
        }
        finally
        {
            AssertEmptyRingBuffer(ringBuffer);
        }
    }

    [Test]
    public void ShouldNotTryPublishEventsWhenBatchSizeIsNegative()
    {
        var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);

        try
        {
            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(-1));
        }
        finally
        {
            AssertEmptyRingBuffer(ringBuffer);
        }
    }

    [Test]
    public void ShouldAddAndRemoveSequences()
    {
        var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 16);

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

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(31)]
    [TestCase(32)]
    [TestCase(int.MaxValue)]
    [TestCase(int.MaxValue +1L)]
    public void ShouldGetEventFromSequence(long sequence)
    {
        var index = 0;
        var ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(index++), 32);

        var evt = ringBuffer[sequence];

        var expectedIndex = sequence % 32;
        Assert.That(evt.Value, Is.EqualTo(expectedIndex));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(31)]
    [TestCase(32)]
    [TestCase(int.MaxValue)]
    [TestCase(int.MaxValue +1L)]
    public void ShouldGetEventSpanFromSequence_1(long sequence)
    {
        var index = 0;
        var ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(index++), 32);

        var span = ringBuffer[sequence, sequence];

        Assert.That(span.Length, Is.EqualTo(1));

        var expectedIndex = sequence % 32;
        Assert.That(span[0].Value, Is.EqualTo(expectedIndex));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(31)]
    [TestCase(32)]
    [TestCase(int.MaxValue)]
    [TestCase(int.MaxValue +1L)]
    public void ShouldGetEventSpanFromSequence_2(long sequence)
    {
        var init = 0;
        var ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(init++), 32);

        var span = ringBuffer[sequence, sequence + 31];

        var expectedStartIndex = sequence % 32;
        var expectedEndIndex = 31;

        Assert.That(span.Length, Is.EqualTo(1 + expectedEndIndex - expectedStartIndex));

        for (var index = 0; index < span.Length; index++)
        {
            Assert.That(span[index].Value, Is.EqualTo(expectedStartIndex + index));
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(31)]
    [TestCase(32)]
    [TestCase(int.MaxValue)]
    [TestCase(int.MaxValue +1L)]
    public void ShouldGetEventBatchFromSequence_1(long sequence)
    {
        var index = 0;
        var ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(index++), 32);

        var span = ringBuffer.GetBatch(sequence, sequence);

        Assert.That(span.Length, Is.EqualTo(1));

        var expectedIndex = sequence % 32;
        Assert.That(span[0].Value, Is.EqualTo(expectedIndex));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(31)]
    [TestCase(32)]
    [TestCase(int.MaxValue)]
    [TestCase(int.MaxValue +1L)]
    public void ShouldGetEventBatchFromSequence_2(long sequence)
    {
        var init = 0;
        var ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(init++), 32);

        var span = ringBuffer.GetBatch(sequence, sequence + 31);

        var expectedStartIndex = sequence % 32;
        var expectedEndIndex = 31;

        Assert.That(span.Length, Is.EqualTo(1 + expectedEndIndex - expectedStartIndex));

        for (var index = 0; index < span.Length; index++)
        {
            Assert.That(span[index].Value, Is.EqualTo(expectedStartIndex + index));
        }
    }

    private static void AssertEmptyRingBuffer(RingBuffer<object[]> ringBuffer)
    {
        Assert.That(ringBuffer[0][0], Is.EqualTo(null));
        Assert.That(ringBuffer[1][0], Is.EqualTo(null));
        Assert.That(ringBuffer[2][0], Is.EqualTo(null));
        Assert.That(ringBuffer[3][0], Is.EqualTo(null));
    }

    private class TestWaiter
    {
        private readonly Barrier _barrier;
        private readonly SequenceBarrier _sequenceBarrier;
        private readonly long _initialSequence;
        private readonly long _toWaitForSequence;
        private readonly RingBuffer<StubEvent> _ringBuffer;

        public TestWaiter(Barrier barrier, SequenceBarrier sequenceBarrier, RingBuffer<StubEvent> ringBuffer, long initialSequence, long toWaitForSequence)
        {
            _barrier = barrier;
            _sequenceBarrier = sequenceBarrier;
            _ringBuffer = ringBuffer;
            _initialSequence = initialSequence;
            _toWaitForSequence = toWaitForSequence;
        }

        public List<StubEvent> Call()
        {
            _barrier.SignalAndWait();
            _sequenceBarrier.WaitForPublishedSequence(_toWaitForSequence);

            var events = new List<StubEvent>();
            for (var l = _initialSequence; l <= _toWaitForSequence; l++)
            {
                events.Add(_ringBuffer[l]);
            }

            return events;
        }
    }
}
