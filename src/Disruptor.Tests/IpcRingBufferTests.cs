using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture]
public class IpcRingBufferTests : IDisposable
{
    private readonly IpcRingBuffer<StubUnmanagedEvent> _ringBuffer;
    private readonly IpcSequenceBarrier _sequenceBarrier;
    private readonly CursorFollower _cursorFollower;

    public IpcRingBufferTests()
    {
        var memory = IpcRingBufferMemory.CreateTemporary(32, initializer: _ => new StubUnmanagedEvent(-1));
        _ringBuffer = new IpcRingBuffer<StubUnmanagedEvent>(memory, new YieldingWaitStrategy(), true);
        _sequenceBarrier = _ringBuffer.NewBarrier();
        _cursorFollower = CursorFollower.StartNew(_ringBuffer);
        _ringBuffer.SetGatingSequences(_cursorFollower.SequencePointer);
    }

    public void Dispose()
    {
        _cursorFollower.Dispose();
        _ringBuffer.Dispose();
    }

    [Test]
    public void ShouldClaimAndGet()
    {
        Assert.That(_ringBuffer.Cursor, Is.EqualTo(Sequence.InitialCursorValue));

        var expectedEvent = new StubUnmanagedEvent(2701);

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

        var expectedEvent = new StubUnmanagedEvent(2701);

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

    private Task<List<StubUnmanagedEvent>> GetEvents(long initial, long toWaitFor)
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
        var memory = IpcRingBufferMemory.CreateTemporary(4, initializer: _ => new StubUnmanagedEvent(-1));
        using var ringBuffer = new IpcRingBuffer<StubUnmanagedEvent>(memory, new YieldingWaitStrategy(), true);
        var sequence = ringBuffer.NewSequence();
        ringBuffer.SetGatingSequences(sequence);

        for (var i = 0; i <= 3; i++)
        {
            ringBuffer.PublishEvent().Dispose();
        }

        Assert.That(!ringBuffer.TryNext(out _));
    }

    [Test]
    public void ShouldReturnFalseIfBufferIsFull()
    {
        var sequence = _ringBuffer.NewSequence();
        _ringBuffer.SetGatingSequences(_cursorFollower.SequencePointer, sequence);

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
        var memory = IpcRingBufferMemory.CreateTemporary(ringBufferSize, initializer: _ => new StubUnmanagedEvent(-1));
        using var ringBuffer = new IpcRingBuffer<StubUnmanagedEvent>(memory, new YieldingWaitStrategy(), true);
        var processor = new TestIpcEventProcessor<StubUnmanagedEvent>(ringBuffer.NewBarrier(), ringBuffer.NewSequence());
        ringBuffer.SetGatingSequences(processor.SequencePointer);

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
        processor.Start();

        // Check producer completes
        Assert.That(task.Wait(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public void ShouldPublishEvent()
    {
        using (var scope = _ringBuffer.PublishEvent())
        {
            scope.Event().Value = (int)scope.Sequence;
        }

        using (var scope = _ringBuffer.PublishEvent())
        {
            scope.Event().Value = (int)scope.Sequence;
        }

        Assert.That(_ringBuffer, IsIpcRingBuffer.WithEvents(new StubUnmanagedEvent(0), new StubUnmanagedEvent(1)));
    }

    [Test]
    public void ShouldPublishEvents()
    {
        using (var scope = _ringBuffer.PublishEvents(2))
        {
            scope.Event(0).Value = (int)scope.StartSequence;
            scope.Event(1).Value = (int)scope.StartSequence + 1;
        }

        Assert.That(_ringBuffer, IsIpcRingBuffer.WithEvents(new StubUnmanagedEvent(0), new StubUnmanagedEvent(1), new StubUnmanagedEvent(-1), new StubUnmanagedEvent(-1)));

        using (var scope = _ringBuffer.PublishEvents(2))
        {
            scope.Event(0).Value = (int)scope.StartSequence;
            scope.Event(1).Value = (int)scope.StartSequence + 1;
        }

        Assert.That(_ringBuffer, IsIpcRingBuffer.WithEvents(new StubUnmanagedEvent(0), new StubUnmanagedEvent(1), new StubUnmanagedEvent(2), new StubUnmanagedEvent(3)));
    }

    [Test]
    public void ShouldNotPublishEventsIfBatchIsLargerThanRingBuffer()
    {
        try
        {
            Assert.Throws<ArgumentException>(() => _ringBuffer.PublishEvents(_ringBuffer.BufferSize + 1));
        }
        finally
        {
            AssertEmptyRingBuffer(_ringBuffer);
        }
    }

    [Test]
    public void ShouldNotPublishEventsWhenBatchSizeIs0()
    {
        try
        {
            Assert.Throws<ArgumentException>(() => _ringBuffer.PublishEvents(0));
        }
        finally
        {
            AssertEmptyRingBuffer(_ringBuffer);
        }
    }

    [Test]
    public void ShouldNotPublishEventsWhenBatchSizeIsNegative()
    {
        try
        {
            Assert.Throws<ArgumentException>(() => _ringBuffer.PublishEvents(-1));
        }
        finally
        {
            AssertEmptyRingBuffer(_ringBuffer);
        }
    }

    [Test]
    public void ShouldAddAndRemoveSequences()
    {
        var sequenceThree = _ringBuffer.NewSequence();
        var sequenceSeven = _ringBuffer.NewSequence();
        _ringBuffer.SetGatingSequences(sequenceThree, sequenceSeven);

        for (var i = 0; i < 10; i++)
        {
            _ringBuffer.Publish(_ringBuffer.Next());
        }

        sequenceThree.SetValue(3);
        sequenceSeven.SetValue(7);
        Assert.That(_ringBuffer.GetMinimumGatingSequence(), Is.EqualTo(3L));

        _ringBuffer.SetGatingSequences(sequenceSeven);
        Assert.That(_ringBuffer.GetMinimumGatingSequence(), Is.EqualTo(7L));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(31)]
    [TestCase(32)]
    [TestCase(int.MaxValue)]
    [TestCase(int.MaxValue +1L)]
    public void ShouldGetEventFromSequence(long sequence)
    {
        var memory = IpcRingBufferMemory.CreateTemporary(32, initializer: x => new StubUnmanagedEvent(x));
        using var ringBuffer = new IpcRingBuffer<StubUnmanagedEvent>(memory, new YieldingWaitStrategy(), true);

        var evt = ringBuffer[sequence];

        var expectedIndex = sequence % 32;
        Assert.That(evt.Value, Is.EqualTo(expectedIndex));
    }

    [Test]
    public void ShouldSetAndGetGatingSequences()
    {
        var sequence = _ringBuffer.NewSequence();
        sequence.SetValue(100);

        _ringBuffer.SetGatingSequences(sequence);

        var sequences = _ringBuffer.GetGatingSequences();
        Assert.That(sequences, Is.EqualTo(new[] { sequence }));
    }

    private static void AssertEmptyRingBuffer(IpcRingBuffer<StubUnmanagedEvent> ringBuffer)
    {
        for (var i = 0; i < ringBuffer.BufferSize; i++)
        {
            Assert.That(ringBuffer[i].Value, Is.EqualTo(-1));
        }
    }

    private class TestWaiter
    {
        private readonly Barrier _barrier;
        private readonly IpcSequenceBarrier _sequenceBarrier;
        private readonly long _initialSequence;
        private readonly long _toWaitForSequence;
        private readonly IpcRingBuffer<StubUnmanagedEvent> _ringBuffer;

        public TestWaiter(Barrier barrier, IpcSequenceBarrier sequenceBarrier, IpcRingBuffer<StubUnmanagedEvent> ringBuffer, long initialSequence, long toWaitForSequence)
        {
            _barrier = barrier;
            _sequenceBarrier = sequenceBarrier;
            _ringBuffer = ringBuffer;
            _initialSequence = initialSequence;
            _toWaitForSequence = toWaitForSequence;
        }

        public List<StubUnmanagedEvent> Call()
        {
            _barrier.SignalAndWait();

            // Because the wait strategy is non-blocking, the consumer must ensure that the sequence is published.
            var spinWait = new SpinWait();
            while (_sequenceBarrier.WaitForPublishedSequence(_toWaitForSequence).UnsafeAvailableSequence < _toWaitForSequence)
            {
                spinWait.SpinOnce();
            }

            var events = new List<StubUnmanagedEvent>();
            for (var l = _initialSequence; l <= _toWaitForSequence; l++)
            {
                events.Add(_ringBuffer[l]);
            }

            return events;
        }
    }
}
