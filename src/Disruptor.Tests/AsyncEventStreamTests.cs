using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture]
public class AsyncEventStreamTests
{
    private readonly RingBuffer<StubEvent> _ringBuffer;

    public AsyncEventStreamTests()
    {
        _ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(0), new SingleProducerSequencer(16, new AsyncWaitStrategy()));
    }

    [Test]
    public async Task ShouldProcessEventsPublishedBeforeIteration()
    {
        // Arrange
        using var stream = _ringBuffer.NewAsyncEventStream();
        var publishedValues = new List<int>();

        for (var i = 0; i < _ringBuffer.BufferSize; i++)
        {
            publishedValues.Add(i);
            _ringBuffer.PublishStubEvent(i);
        }

        var processedValues = new List<int>();

        // Act
        await foreach (var batch in stream.TakeEvents(_ringBuffer.BufferSize))
        {
            foreach (var data in batch)
            {
                processedValues.Add(data.Value);
            }
        }

        // Assert
        Assert.That(processedValues, Is.EquivalentTo(publishedValues));
    }

    [Test]
    public void ShouldProcessEventsPublishedDuringIteration()
    {
        // Arrange
        using var stream = _ringBuffer.NewAsyncEventStream();
        var publishedValues = new List<int>();
        var processedValues = new List<int>();
        var publicationChunkCount = 5;

        var task = Task.Run(async () =>
        {
            await foreach (var batch in stream.TakeEvents(publicationChunkCount * _ringBuffer.BufferSize))
            {
                foreach (var data in batch)
                {
                    processedValues.Add(data.Value);
                }
            }
        });

        // Act
        for (var i = 0; i < publicationChunkCount; i++)
        {
            Thread.Sleep(10);

            for (var y = 0; y < _ringBuffer.BufferSize; y++)
            {
                var value = i * _ringBuffer.BufferSize + y;
                publishedValues.Add(value);
                _ringBuffer.PublishStubEvent(value);
            }
        }
        // Assert
        Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(2)));
        Assert.That(processedValues, Is.EquivalentTo(publishedValues));
    }

    [Test]
    public void ShouldProcessEventsFromMultipleStreams()
    {
        // Arrange
        using var stream1 = _ringBuffer.NewAsyncEventStream();
        using var stream2 = _ringBuffer.NewAsyncEventStream();
        var publicationChunkCount = 20;
        var publishedValues = new List<int>();
        var processedValues1 = new List<int>();
        var processedValues2 = new List<int>();

        var task1 = Task.Run(async () =>
        {
            await foreach (var batch in stream1.TakeEvents(publicationChunkCount * _ringBuffer.BufferSize))
            {
                foreach (var data in batch)
                {
                    processedValues1.Add(data.Value);
                }
            }
        });

        var task2 = Task.Run(async () =>
        {
            await foreach (var batch in stream2.TakeEvents(publicationChunkCount * _ringBuffer.BufferSize))
            {
                foreach (var data in batch)
                {
                    processedValues2.Add(data.Value);
                }
            }
        });

        // Act
        for (var i = 0; i < publicationChunkCount; i++)
        {
            Thread.Sleep(5);

            for (var y = 0; y < _ringBuffer.BufferSize; y++)
            {
                var value = i * _ringBuffer.BufferSize + y;
                publishedValues.Add(value);
                _ringBuffer.PublishStubEvent(value);
            }
        }

        // Assert
        Assert.IsTrue(task1.Wait(TimeSpan.FromSeconds(2)));
        Assert.IsTrue(task2.Wait(TimeSpan.FromSeconds(2)));
        Assert.That(processedValues1, Is.EquivalentTo(publishedValues));
        Assert.That(processedValues2, Is.EquivalentTo(publishedValues));
    }

    [Test]
    public void ShouldResetEnumeratorSequence()
    {
        // Arrange
        using var stream = _ringBuffer.NewAsyncEventStream();

        _ringBuffer.PublishStubEvent(0);
        _ringBuffer.PublishStubEvent(1);
        _ringBuffer.PublishStubEvent(2);

        stream.ResetNextEnumeratorSequence();

        var processedValues = new List<int>();

        // Act
        var task = Task.Run(async () =>
        {
            await foreach (var batch in stream)
            {
                processedValues.AddRange(batch.AsEnumerable().Select(x => x.Value));
                return;
            }
        });

        _ringBuffer.PublishStubEvent(3);

        // Assert
        Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(2)));
        Assert.That(processedValues, Is.EquivalentTo(new[] { 3 }));
    }

    [Test]
    public void ShouldAddGatingSequence()
    {
        using var stream = _ringBuffer.NewAsyncEventStream();

        for (var i = 0; i < _ringBuffer.BufferSize; i++)
        {
            _ringBuffer.PublishStubEvent(0);
        }

        var canGetNextSequence = _ringBuffer.TryNext(out _);

        Assert.IsFalse(canGetNextSequence);
    }

    [Test]
    public void ShouldRemoveGatingSequenceOnDispose()
    {
        using var stream = _ringBuffer.NewAsyncEventStream();

        for (var i = 0; i < _ringBuffer.BufferSize; i++)
        {
            _ringBuffer.PublishStubEvent(0);
        }

        stream.Dispose();

        var canGetNextSequence = _ringBuffer.TryNext(out _);

        Assert.IsTrue(canGetNextSequence);
    }

    [Test]
    public void ShouldStopIterationOnCancellation()
    {
        var stream = _ringBuffer.NewAsyncEventStream();
        var cancellationTokenSource = new CancellationTokenSource();
        var processingSignal = new ManualResetEventSlim();

        var task = Task.Run(async () =>
        {
            await foreach (var batch in stream.WithCancellation(cancellationTokenSource.Token))
            {
                processingSignal.Set();
            }
        });

        _ringBuffer.PublishStubEvent(0);

        Assert.IsTrue(processingSignal.Wait(TimeSpan.FromSeconds(2)));

        cancellationTokenSource.Cancel();

        AssertIsCancelled(task);
    }

    [Test]
    public void ShouldStopIterationOnStreamDispose()
    {
        var stream = _ringBuffer.NewAsyncEventStream();
        var processingSignal = new ManualResetEventSlim();

        var task = Task.Run(async () =>
        {
            await foreach (var batch in stream)
            {
                processingSignal.Set();
            }
        });

        _ringBuffer.PublishStubEvent(0);

        Assert.IsTrue(processingSignal.Wait(TimeSpan.FromSeconds(2)));

        stream.Dispose();

        AssertIsCancelled(task);
    }

    private static void AssertIsCancelled(Task task)
    {
        var exception = Assert.Catch<AggregateException>(() => task.Wait(TimeSpan.FromSeconds(2)));
        Assert.IsInstanceOf<TaskCanceledException>(exception!.InnerException);
    }
}
