using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class BatchingTest
    {
        public static IEnumerable<object[]> GenerateData()
        {
            yield return new object[] { ProducerType.Multi };
            yield return new object[] { ProducerType.Single };
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
            public Volatile.Long Processed;

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
                    Thread.Sleep(0); // LockSupport.parkNanos(1);
                }

                Processed.WriteFullFence(sequence);
            }
        }

        [TestCaseSource(nameof(GenerateData))]
        public void ShouldBatch(ProducerType producerType)
        {
            var d = new Disruptor<LongEvent>(() => new LongEvent(), 2048, TaskScheduler.Current, producerType, new SleepingWaitStrategy());

            var handler1 = new ParallelEventHandler(1, 0);
            var handler2 = new ParallelEventHandler(1, 1);

            d.HandleEventsWith(handler1, handler2);

            var buffer = d.Start();

            IEventTranslator<LongEvent> translator = new EventTranslator<LongEvent>();

            const int eventCount = 10000;
            for (var i = 0; i < eventCount; i++)
            {
                buffer.PublishEvent(translator);
            }

            while (handler1.Processed.ReadAcquireFence() != eventCount - 1 ||
                   handler2.Processed.ReadAcquireFence() != eventCount - 1)
            {
                Thread.Sleep(1);
            }

            Assert.That(handler1.PublishedValue, Is.EqualTo((long)eventCount - 2));
            Assert.That(handler1.EventCount, Is.EqualTo((long)eventCount / 2));
            Assert.That(handler2.PublishedValue, Is.EqualTo((long)eventCount - 1));
            Assert.That(handler2.EventCount, Is.EqualTo((long)eventCount / 2));
        }

        private class EventTranslator<T> : IEventTranslator<LongEvent>
        {
            public void TranslateTo(LongEvent eventData, long sequence)
            {
                eventData.Value = sequence;
            }
        }
    }
}
