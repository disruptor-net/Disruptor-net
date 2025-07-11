using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture(ProducerType.Single)]
[TestFixture(ProducerType.Multi)]
public class BatchingTests
{
    private readonly ProducerType _producerType;

    public BatchingTests(ProducerType producerType)
    {
        _producerType = producerType;
    }

    private class ParallelEventHandler : IEventHandler<LongEvent>
    {
        private readonly long _mask;
        private readonly long _ordinal;
        private const int _batchSize = 10;

        public long EventCount;
        public long BatchCount;
        public long PublishedValue;
        public long TempValue;
        public long Processed;

        public ParallelEventHandler(long mask, long ordinal)
        {
            _mask = mask;
            _ordinal = ordinal;
        }

        public void OnEvent(LongEvent @event, long sequence, bool endOfBatch)
        {
            if ((sequence & _mask) == _ordinal)
            {
                EventCount++;
                TempValue = @event.Value;
            }

            if (endOfBatch || ++BatchCount >= _batchSize)
            {
                PublishedValue = TempValue;
                BatchCount = 0;
            }
            else
            {
                Thread.Yield();
            }

            Volatile.Write(ref Processed, sequence);
        }
    }

    [Test]
    public void ShouldBatch()
    {
        using var d = new Disruptor<LongEvent>(() => new LongEvent(), 2048, TaskScheduler.Current, _producerType, new SleepingWaitStrategy());

        var handler1 = new ParallelEventHandler(1, 0);
        var handler2 = new ParallelEventHandler(1, 1);

        d.HandleEventsWith(handler1, handler2);
        d.Start();

        var buffer = d.RingBuffer;

        const int eventCount = 10000;
        for (var i = 0; i < eventCount; i++)
        {
            using (var scope = buffer.PublishEvent())
            {
                scope.Event().Value = scope.Sequence;
            }
        }

        while (Volatile.Read(ref handler1.Processed) != eventCount - 1 ||
               Volatile.Read(ref handler2.Processed) != eventCount - 1)
        {
            Thread.Sleep(1);
        }

        Assert.That(handler1.PublishedValue, Is.EqualTo((long)eventCount - 2));
        Assert.That(handler1.EventCount, Is.EqualTo((long)eventCount / 2));
        Assert.That(handler2.PublishedValue, Is.EqualTo((long)eventCount - 1));
        Assert.That(handler2.EventCount, Is.EqualTo((long)eventCount / 2));
    }
}
