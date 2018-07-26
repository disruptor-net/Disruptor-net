using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Tests.Support;
using NUnit.Framework;
using static Disruptor.Tests.RingBufferEqualsConstraint;

namespace Disruptor.Tests
{
    [TestFixture]
    public class RingBufferTests
    {
        private RingBuffer<StubEvent> _ringBuffer;
        private ISequenceBarrier _sequenceBarrier;

        [SetUp]
        public void SetUp()
        {
            _ringBuffer = RingBuffer<StubEvent>.CreateMultiProducer(() => new StubEvent(-1), 32);
            _sequenceBarrier = _ringBuffer.NewBarrier();
            _ringBuffer.AddGatingSequences(new NoOpEventProcessor<StubEvent>(_ringBuffer).Sequence);
        }

        [Test]
        public void ShouldClaimAndGet()
        {
            Assert.AreEqual(Sequence.InitialCursorValue, _ringBuffer.Cursor);

            var expectedEvent = new StubEvent(2701);

            var claimSequence = _ringBuffer.Next();
            var oldEvent = _ringBuffer[claimSequence];
            oldEvent.Copy(expectedEvent);
            _ringBuffer.Publish(claimSequence);

            var sequence = _sequenceBarrier.WaitFor(0);
            Assert.AreEqual(0, sequence);

            var evt = _ringBuffer[sequence];
            Assert.AreEqual(expectedEvent, evt);

            Assert.AreEqual(0L, _ringBuffer.Cursor);
        }

        [Test]
        public void ShouldClaimAndGetInSeparateThread()
        {
            var events = GetEvents(0, 0);

            var expectedEvent = new StubEvent(2701);

            var sequence = _ringBuffer.Next();
            var oldEvent = _ringBuffer[sequence];
            oldEvent.Copy(expectedEvent);
            _ringBuffer.PublishEvent(StubEvent.Translator, expectedEvent.Value, expectedEvent.TestString);

            Assert.AreEqual(expectedEvent, events.Result[0]);
        }

        [Test]
        public void ShouldClaimAndGetMultipleMessages()
        {
            var numEvents = _ringBuffer.BufferSize;
            for (var i = 0; i < numEvents; i++)
            {
                _ringBuffer.PublishEvent(StubEvent.Translator, i, "");
            }

            var expectedSequence = numEvents - 1;
            var available = _sequenceBarrier.WaitFor(expectedSequence);
            Assert.AreEqual(expectedSequence, available);

            for (var i = 0; i < numEvents; i++)
            {
                Assert.AreEqual(i, _ringBuffer[i].Value);
            }
        }

        [Test]
        public void ShouldWrap()
        {
            var numEvents = _ringBuffer.BufferSize;
            const int offset = 1000;
            for (var i = 0; i < numEvents + offset; i++)
            {
                _ringBuffer.PublishEvent(StubEvent.Translator, i, "");
            }

            var expectedSequence = numEvents + offset - 1;
            var available = _sequenceBarrier.WaitFor(expectedSequence);
            Assert.AreEqual(expectedSequence, available);

            for (var i = offset; i < numEvents + offset; i++)
            {
                Assert.AreEqual(i, _ringBuffer[i].Value);
            }
        }

        private Task<List<StubEvent>> GetEvents(long initial, long toWaitFor)
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
            var sequence = new Sequence(Sequence.InitialCursorValue);
            var ringBuffer = RingBuffer<StubEvent>.CreateMultiProducer(() => new StubEvent(-1), 4);
            ringBuffer.AddGatingSequences(sequence);

            ringBuffer.PublishEvent(StubEvent.Translator, 0, "0");
            ringBuffer.PublishEvent(StubEvent.Translator, 1, "1");
            ringBuffer.PublishEvent(StubEvent.Translator, 2, "2");
            ringBuffer.PublishEvent(StubEvent.Translator, 3, "3");

            Assert.IsFalse(ringBuffer.TryPublishEvent(StubEvent.Translator, 3, "3"));
        }

        [Test]
        public void ShouldThrowExceptionIfBufferIsFull()
        {
            _ringBuffer.AddGatingSequences(new Sequence(_ringBuffer.BufferSize));

            try
            {
                for (var i = 0; i < _ringBuffer.BufferSize; i++)
                {
                    _ringBuffer.Publish(_ringBuffer.TryNext());
                }
            }
            catch (Exception)
            {
                throw new ApplicationException("Should not of thrown exception");
            }

            try
            {
                _ringBuffer.TryNext();
                throw new ApplicationException("Exception should have been thrown");
            }
            catch (InsufficientCapacityException)
            {
            }
        }

        [Test]
        public void ShouldPreventProducersOvertakingEventProcessorsWrapPoint()
        {
            const int ringBufferSize = 4;
            var mre = new ManualResetEvent(false);
            var producerComplete = false;
            var ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(-1), ringBufferSize);
            var processor = new TestEventProcessor(ringBuffer.NewBarrier());
            ringBuffer.AddGatingSequences(processor.Sequence);

            var thread = new Thread(
                () =>
                {
                    for (var i = 0; i <= ringBufferSize; i++) // produce 5 events
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

                    producerComplete = true;
                });

            thread.Start();

            mre.WaitOne();
            Assert.That(ringBuffer.Cursor, Is.EqualTo(ringBufferSize - 1));
            Assert.IsFalse(producerComplete);

            processor.Run();
            thread.Join();

            Assert.IsTrue(producerComplete);
        }

        [Test]
        public void ShouldPublishEvent()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslator<object[]> translator = new NoArgEventTranslator();

            ringBuffer.PublishEvent(translator);
            ringBuffer.TryPublishEvent(translator);

            Assert.That(ringBuffer, IsRingBufferWithEvents(0L, 1L));
        }

        [Test]
        public void ShouldPublishEventOneArg()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorOneArg<object[], string> translator = new OneArgEventTranslator();

            ringBuffer.PublishEvent(translator, "Foo");
            ringBuffer.TryPublishEvent(translator, "Foo");

            Assert.That(ringBuffer, IsRingBufferWithEvents("Foo-0", "Foo-1"));
        }

        [Test]
        public void ShouldPublishEventTwoArg()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorTwoArg<object[], string, string> translator = new TwoArgEventTranslator();

            ringBuffer.PublishEvent(translator, "Foo", "Bar");
            ringBuffer.TryPublishEvent(translator, "Foo", "Bar");

            Assert.That(ringBuffer, IsRingBufferWithEvents("FooBar-0", "FooBar-1"));
        }

        [Test]
        public void ShouldPublishEventThreeArg()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorThreeArg<object[], string, string, string> translator = new ThreeArgEventTranslator();

            ringBuffer.PublishEvent(translator, "Foo", "Bar", "Baz");
            ringBuffer.TryPublishEvent(translator, "Foo", "Bar", "Baz");

            Assert.That(ringBuffer, IsRingBufferWithEvents("FooBarBaz-0", "FooBarBaz-1"));
        }

        [Test]
        public void ShouldPublishEventVarArg()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorVararg<object[]> translator = new VarArgEventTranslator();

            ringBuffer.PublishEvent(translator, "Foo", "Bar", "Baz", "Bam");
            ringBuffer.TryPublishEvent(translator, "Foo", "Bar", "Baz", "Bam");

            Assert.That(ringBuffer, IsRingBufferWithEvents("FooBarBazBam-0", "FooBarBazBam-1"));
        }

        [Test]
        public void ShouldPublishEvents()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslator<object[]> eventTranslator = new NoArgEventTranslator();
            var translators = new[] { eventTranslator, eventTranslator };

            ringBuffer.PublishEvents(translators);
            Assert.IsTrue(ringBuffer.TryPublishEvents(translators));

            Assert.That(ringBuffer, IsRingBufferWithEvents(0L, 1L, 2L, 3L));
        }

        [Test]
        public void ShouldNotPublishEventsIfBatchIsLargerThanRingBuffer()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslator<object[]> eventTranslator = new NoArgEventTranslator();
            var translators =
                new[] { eventTranslator, eventTranslator, eventTranslator, eventTranslator, eventTranslator };

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translators));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldPublishEventsWithBatchSizeOfOne()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslator<object[]> eventTranslator = new NoArgEventTranslator();
            var translators =
                new[] { eventTranslator, eventTranslator, eventTranslator };

            ringBuffer.PublishEvents(translators, 0, 1);
            Assert.IsTrue(ringBuffer.TryPublishEvents(translators, 0, 1));

            Assert.That(ringBuffer, IsRingBufferWithEvents(0L, 1L, null, null));
        }

        [Test]
        public void ShouldPublishEventsWithinBatch()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslator<object[]> eventTranslator = new NoArgEventTranslator();
            var translators =
                new[] { eventTranslator, eventTranslator, eventTranslator };

            ringBuffer.PublishEvents(translators, 1, 2);
            Assert.IsTrue(ringBuffer.TryPublishEvents(translators, 1, 2));

            Assert.That(ringBuffer, IsRingBufferWithEvents(0L, 1L, 2L, 3L));
        }

        [Test]
        public void ShouldPublishEventsOneArg()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorOneArg<object[], string> translator = new OneArgEventTranslator();

            ringBuffer.PublishEvents(translator, new[] { "Foo", "Foo" });
            Assert.IsTrue(ringBuffer.TryPublishEvents(translator, new[] { "Foo", "Foo" }));

            Assert.That(ringBuffer, IsRingBufferWithEvents("Foo-0", "Foo-1", "Foo-2", "Foo-3"));
        }

        [Test]
        public void ShouldNotPublishEventsOneArgIfBatchIsLargerThanRingBuffer()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorOneArg<object[], string> translator = new OneArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, new[] { "Foo", "Foo", "Foo", "Foo", "Foo" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldPublishEventsOneArgBatchSizeOfOne()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorOneArg<object[], string> translator = new OneArgEventTranslator();

            ringBuffer.PublishEvents(translator, 0, 1, new[] { "Foo", "Foo" });
            Assert.IsTrue(ringBuffer.TryPublishEvents(translator, 0, 1, new[] { "Foo", "Foo" }));

            Assert.That(ringBuffer, IsRingBufferWithEvents("Foo-0", "Foo-1", null, null));
        }

        [Test]
        public void ShouldPublishEventsOneArgWithinBatch()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorOneArg<object[], string> translator = new OneArgEventTranslator();

            ringBuffer.PublishEvents(translator, 1, 2, new[] { "Foo", "Foo", "Foo" });
            Assert.IsTrue(ringBuffer.TryPublishEvents(translator, 1, 2, new[] { "Foo", "Foo", "Foo" }));

            Assert.That(ringBuffer, IsRingBufferWithEvents("Foo-0", "Foo-1", "Foo-2", "Foo-3"));
        }

        [Test]
        public void ShouldPublishEventsTwoArg()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorTwoArg<object[], string, string> translator = new TwoArgEventTranslator();

            ringBuffer.PublishEvents(translator, new[] { "Foo", "Foo" }, new[] { "Bar", "Bar" });
            ringBuffer.TryPublishEvents(translator, new[] { "Foo", "Foo" }, new[] { "Bar", "Bar" });

            Assert.That(ringBuffer, IsRingBufferWithEvents("FooBar-0", "FooBar-1", "FooBar-2", "FooBar-3"));
        }

        [Test]
        public void ShouldNotPublishEventsITwoArgIfBatchSizeIsBiggerThanRingBuffer()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorTwoArg<object[], string, string> translator = new TwoArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(
                                                     translator,
                                                     new[] { "Foo", "Foo", "Foo", "Foo", "Foo" },
                                                     new[] { "Bar", "Bar", "Bar", "Bar", "Bar" }));
                ;
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldPublishEventsTwoArgWithBatchSizeOfOne()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorTwoArg<object[], string, string> translator = new TwoArgEventTranslator();

            ringBuffer.PublishEvents(translator, 0, 1, new[] { "Foo0", "Foo1" }, new[] { "Bar0", "Bar1" });
            ringBuffer.TryPublishEvents(translator, 0, 1, new[] { "Foo2", "Foo3" }, new[] { "Bar2", "Bar3" });

            Assert.That(ringBuffer, IsRingBufferWithEvents("Foo0Bar0-0", "Foo2Bar2-1", null, null));
        }

        [Test]
        public void ShouldPublishEventsTwoArgWithinBatch()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorTwoArg<object[], string, string> translator = new TwoArgEventTranslator();

            ringBuffer.PublishEvents(
                translator, 1, 2, new[] { "Foo0", "Foo1", "Foo2" }, new[] { "Bar0", "Bar1", "Bar2" });
            ringBuffer.TryPublishEvents(
                translator, 1, 2, new[] { "Foo3", "Foo4", "Foo5" }, new[] { "Bar3", "Bar4", "Bar5" });

            Assert.That(ringBuffer, IsRingBufferWithEvents("Foo1Bar1-0", "Foo2Bar2-1", "Foo4Bar4-2", "Foo5Bar5-3"));
        }

        [Test]
        public void ShouldPublishEventsThreeArg()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorThreeArg<object[], string, string, string> translator = new ThreeArgEventTranslator();

            ringBuffer.PublishEvents(
                translator, new[] { "Foo", "Foo" }, new[] { "Bar", "Bar" }, new[] { "Baz", "Baz" });
            ringBuffer.TryPublishEvents(
                translator, new[] { "Foo", "Foo" }, new[] { "Bar", "Bar" }, new[] { "Baz", "Baz" });

            Assert.That(ringBuffer, IsRingBufferWithEvents("FooBarBaz-0", "FooBarBaz-1", "FooBarBaz-2", "FooBarBaz-3"));
        }

        [Test]
        public void ShouldNotPublishEventsThreeArgIfBatchIsLargerThanRingBuffer()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorThreeArg<object[], string, string, string> translator = new ThreeArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(
                                                     translator,
                                                     new[] { "Foo", "Foo", "Foo", "Foo", "Foo" },
                                                     new[] { "Bar", "Bar", "Bar", "Bar", "Bar" },
                                                     new[] { "Baz", "Baz", "Baz", "Baz", "Baz" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldPublishEventsThreeArgBatchSizeOfOne()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorThreeArg<object[], string, string, string> translator = new ThreeArgEventTranslator();

            ringBuffer.PublishEvents(
                translator, 0, 1, new[] { "Foo", "Foo" }, new[] { "Bar", "Bar" }, new[] { "Baz", "Baz" });
            ringBuffer.TryPublishEvents(
                translator, 0, 1, new[] { "Foo", "Foo" }, new[] { "Bar", "Bar" }, new[] { "Baz", "Baz" });

            Assert.That(ringBuffer, IsRingBufferWithEvents("FooBarBaz-0", "FooBarBaz-1", null, null));
        }

        [Test]
        public void ShouldPublishEventsThreeArgWithinBatch()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorThreeArg<object[], string, string, string> translator = new ThreeArgEventTranslator();

            ringBuffer.PublishEvents(
                translator, 1, 2, new[] { "Foo0", "Foo1", "Foo2" }, new[] { "Bar0", "Bar1", "Bar2" },
                new[] { "Baz0", "Baz1", "Baz2" }
                );
            Assert.IsTrue(
                ringBuffer.TryPublishEvents(
                    translator, 1, 2, new[] { "Foo3", "Foo4", "Foo5" }, new[] { "Bar3", "Bar4", "Bar5" },
                    new[] { "Baz3", "Baz4", "Baz5" }));

            Assert.That(ringBuffer, IsRingBufferWithEvents("Foo1Bar1Baz1-0", "Foo2Bar2Baz2-1", "Foo4Bar4Baz4-2", "Foo5Bar5Baz5-3"));
        }

        [Test]
        public void ShouldPublishEventsVarArg()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorVararg<object[]> translator = new VarArgEventTranslator();

            ringBuffer.PublishEvents(translator, new[] { "Foo", "Bar", "Baz", "Bam" }, new[] { "Foo", "Bar", "Baz", "Bam" });
            Assert.IsTrue(ringBuffer.TryPublishEvents(translator, new[] { "Foo", "Bar", "Baz", "Bam" }, new[] { "Foo", "Bar", "Baz", "Bam" }));

            Assert.That(ringBuffer, IsRingBufferWithEvents("FooBarBazBam-0", "FooBarBazBam-1", "FooBarBazBam-2", "FooBarBazBam-3"));
        }

        [Test]
        public void ShouldNotPublishEventsVarArgIfBatchIsLargerThanRingBuffer()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorVararg<object[]> translator = new VarArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(
                                                     translator,
                                                     new[] { "Foo", "Bar", "Baz", "Bam" },
                                                     new[] { "Foo", "Bar", "Baz", "Bam" },
                                                     new[] { "Foo", "Bar", "Baz", "Bam" },
                                                     new[] { "Foo", "Bar", "Baz", "Bam" },
                                                     new[] { "Foo", "Bar", "Baz", "Bam" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldPublishEventsVarArgBatchSizeOfOne()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorVararg<object[]> translator = new VarArgEventTranslator();

            ringBuffer.PublishEvents(
                translator, 0, 1, new object[] { "Foo", "Bar", "Baz", "Bam" }, new object[] { "Foo", "Bar", "Baz", "Bam" });
            Assert.IsTrue(
                ringBuffer.TryPublishEvents(
                    translator, 0, 1, new object[] { "Foo", "Bar", "Baz", "Bam" }, new object[] { "Foo", "Bar", "Baz", "Bam" }));

            Assert.That(
                ringBuffer, IsRingBufferWithEvents(
                    "FooBarBazBam-0", "FooBarBazBam-1", null, null));
        }

        [Test]
        public void ShouldPublishEventsVarArgWithinBatch()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorVararg<object[]> translator = new VarArgEventTranslator();

            ringBuffer.PublishEvents(
                translator, 1, 2, new object[] { "Foo0", "Bar0", "Baz0", "Bam0" },
                new object[] { "Foo1", "Bar1", "Baz1", "Bam1" },
                new object[] { "Foo2", "Bar2", "Baz2", "Bam2" });
            Assert.IsTrue(
                ringBuffer.TryPublishEvents(
                    translator, 1, 2, new object[] { "Foo3", "Bar3", "Baz3", "Bam3" },
                    new object[] { "Foo4", "Bar4", "Baz4", "Bam4" },
                    new object[] { "Foo5", "Bar5", "Baz5", "Bam5" }));

            Assert.That(
                ringBuffer, IsRingBufferWithEvents(
                    "Foo1Bar1Baz1Bam1-0", "Foo2Bar2Baz2Bam2-1", "Foo4Bar4Baz4Bam4-2", "Foo5Bar5Baz5Bam5-3"));
        }

        [Test]
        public void ShouldNotPublishEventsWhenBatchSizeIs0()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslator<object[]> translator = new NoArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(new[] { translator, translator, translator, translator }, 1, 0));
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
            IEventTranslator<object[]> translator = new NoArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(new[] { translator, translator, translator, translator }, 1, 0));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotPublishEventsWhenBatchExtendsPastEndOfArray()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslator<object[]> translator = new NoArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(new[] { translator, translator, translator }, 1, 3));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotTryPublishEventsWhenBatchExtendsPastEndOfArray()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslator<object[]> translator = new NoArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(new[] { translator, translator, translator }, 1, 3));
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
            IEventTranslator<object[]> translator = new NoArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(new[] { translator, translator, translator, translator }, 1, -1));
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
            IEventTranslator<object[]> translator = new NoArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(new[] { translator, translator, translator, translator }, 1, -1));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotPublishEventsWhenBatchStartsAtIsNegative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslator<object[]> translator = new NoArgEventTranslator();
            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(new[] { translator, translator, translator, translator }, -1, 2));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotTryPublishEventsWhenBatchStartsAtIsNegative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslator<object[]> translator = new NoArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(new[] { translator, translator, translator, translator }, -1, 2));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotPublishEventsOneArgWhenBatchSizeIs0()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorOneArg<object[], string> translator = new OneArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, 1, 0, new[] { "Foo", "Foo" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotTryPublishEventsOneArgWhenBatchSizeIs0()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorOneArg<object[], string> translator = new OneArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, 1, 0, new[] { "Foo", "Foo" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotPublishEventsOneArgWhenBatchExtendsPastEndOfArray()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorOneArg<object[], string> translator = new OneArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, 1, 3, new[] { "Foo", "Foo" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotPublishEventsOneArgWhenBatchSizeIsNegative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorOneArg<object[], string> translator = new OneArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, 1, -1, new[] { "Foo", "Foo" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotPublishEventsOneArgWhenBatchStartsAtIsNegative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorOneArg<object[], string> translator = new OneArgEventTranslator();
            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, -1, 2, new[] { "Foo", "Foo" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotTryPublishEventsOneArgWhenBatchExtendsPastEndOfArray()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorOneArg<object[], string> translator = new OneArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, 1, 3, new[] { "Foo", "Foo" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotTryPublishEventsOneArgWhenBatchSizeIsNegative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorOneArg<object[], string> translator = new OneArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, 1, -1, new[] { "Foo", "Foo" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotTryPublishEventsOneArgWhenBatchStartsAtIsNegative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorOneArg<object[], string> translator = new OneArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, -1, 2, new[] { "Foo", "Foo" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotPublishEventsTwoArgWhenBatchSizeIs0()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorTwoArg<object[], string, string> translator = new TwoArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(
                                                     translator, 1, 0, new[] { "Foo", "Foo" },
                                                     new[] { "Bar", "Bar" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotTryPublishEventsTwoArgWhenBatchSizeIs0()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorTwoArg<object[], string, string> translator = new TwoArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(
                                                     translator, 1, 0, new[] { "Foo", "Foo" },
                                                     new[] { "Bar", "Bar" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotPublishEventsTwoArgWhenBatchExtendsPastEndOfArray()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorTwoArg<object[], string, string> translator = new TwoArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, 1, 3, new[] { "Foo", "Foo" }, new[] { "Bar", "Bar" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotPublishEventsTwoArgWhenBatchSizeIsNegative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorTwoArg<object[], string, string> translator = new TwoArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, 1, -1, new[] { "Foo", "Foo" }, new[] { "Bar", "Bar" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotPublishEventsTwoArgWhenBatchStartsAtIsNegative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorTwoArg<object[], string, string> translator = new TwoArgEventTranslator();
            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, -1, 2, new[] { "Foo", "Foo" }, new[] { "Bar", "Bar" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotTryPublishEventsTwoArgWhenBatchExtendsPastEndOfArray()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorTwoArg<object[], string, string> translator = new TwoArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, 1, 3, new[] { "Foo", "Foo" }, new[] { "Bar", "Bar" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotTryPublishEventsTwoArgWhenBatchSizeIsNegative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorTwoArg<object[], string, string> translator = new TwoArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, 1, -1, new[] { "Foo", "Foo" }, new[] { "Bar", "Bar" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotTryPublishEventsTwoArgWhenBatchStartsAtIsNegative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorTwoArg<object[], string, string> translator = new TwoArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, -1, 2, new[] { "Foo", "Foo" }, new[] { "Bar", "Bar" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotPublishEventsThreeArgWhenBatchSizeIs0()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorThreeArg<object[], string, string, string> translator = new ThreeArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(
                                                     translator, 1, 0, new[] { "Foo", "Foo" },
                                                     new[] { "Bar", "Bar" }, new[] { "Baz", "Baz" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotTryPublishEventsThreeArgWhenBatchSizeIs0()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorThreeArg<object[], string, string, string> translator = new ThreeArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(
                                                     translator, 1, 0, new[] { "Foo", "Foo" },
                                                     new[] { "Bar", "Bar" }, new[] { "Baz", "Baz" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotPublishEventsThreeArgWhenBatchExtendsPastEndOfArray()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorThreeArg<object[], string, string, string> translator = new ThreeArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(
                                                     translator, 1, 3, new[] { "Foo", "Foo" }, new[] { "Bar", "Bar" },
                                                     new[] { "Baz", "Baz" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotPublishEventsThreeArgWhenBatchSizeIsNegative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorThreeArg<object[], string, string, string> translator = new ThreeArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(
                                                     translator, 1, -1, new[] { "Foo", "Foo" }, new[] { "Bar", "Bar" },
                                                     new[] { "Baz", "Baz" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotPublishEventsThreeArgWhenBatchStartsAtIsNegative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorThreeArg<object[], string, string, string> translator = new ThreeArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(
                                                     translator, -1, 2, new[] { "Foo", "Foo" }, new[] { "Bar", "Bar" },
                                                     new[] { "Baz", "Baz" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotTryPublishEventsThreeArgWhenBatchExtendsPastEndOfArray()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorThreeArg<object[], string, string, string> translator = new ThreeArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(
                                                     translator, 1, 3, new[] { "Foo", "Foo" }, new[] { "Bar", "Bar" },
                                                     new[] { "Baz", "Baz" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotTryPublishEventsThreeArgWhenBatchSizeIsNegative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorThreeArg<object[], string, string, string> translator = new ThreeArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(
                                                     translator, 1, -1, new[] { "Foo", "Foo" },
                                                     new[] { "Bar", "Bar" }, new[] { "Baz", "Baz" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotTryPublishEventsThreeArgWhenBatchStartsAtIsNegative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            IEventTranslatorThreeArg<object[], string, string, string> translator = new ThreeArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(
                                                     translator, -1, 2, new[] { "Foo", "Foo" },
                                                     new[] { "Bar", "Bar" }, new[] { "Baz", "Baz" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotPublishEventsVarArgWhenBatchSizeIs0()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            var translator = new VarArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(
                                                     translator, 1, 0, new object[] { "Foo0", "Bar0", "Baz0", "Bam0" },
                                                     new object[] { "Foo1", "Bar1", "Baz1", "Bam1" },
                                                     new object[] { "Foo2", "Bar2", "Baz2", "Bam2" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotTryPublishEventsVarArgWhenBatchSizeIs0()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            var translator = new VarArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(
                                                     translator, 1, 0, new object[] { "Foo0", "Bar0", "Baz0", "Bam0" },
                                                     new object[] { "Foo1", "Bar1", "Baz1", "Bam1" },
                                                     new object[] { "Foo2", "Bar2", "Baz2", "Bam2" }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotPublishEventsVarArgWhenBatchExtendsPastEndOfArray()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            var translator = new VarArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(
                                                     translator, 1, 3, new object[] { "Foo0", "Bar0", "Baz0", "Bam0" },
                                                     new object[] { "Foo1", "Bar1", "Baz1", "Bam1" }, new object[]
                                                     {
                                                         "Foo2", "Bar2",
                                                         "Baz2", "Bam2"
                                                     }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotPublishEventsVarArgWhenBatchSizeIsNegative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            var translator = new VarArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(
                                                     translator, 1, -1, new object[] { "Foo0", "Bar0", "Baz0", "Bam0" },
                                                     new object[] { "Foo1", "Bar1", "Baz1", "Bam1" }, new object[]
                                                     {
                                                         "Foo2", "Bar2",
                                                         "Baz2", "Bam2"
                                                     }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotPublishEventsVarArgWhenBatchStartsAtIsNegative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            var translator = new VarArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(
                                                     translator, -1, 2, new object[] { "Foo0", "Bar0", "Baz0", "Bam0" },
                                                     new object[] { "Foo1", "Bar1", "Baz1", "Bam1" }, new object[]
                                                     {
                                                         "Foo2", "Bar2",
                                                         "Baz2", "Bam2"
                                                     }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotTryPublishEventsVarArgWhenBatchExtendsPastEndOfArray()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            var translator = new VarArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(
                                                     translator, 1, 3, new object[] { "Foo0", "Bar0", "Baz0", "Bam0" },
                                                     new object[] { "Foo1", "Bar1", "Baz1", "Bam1" }, new object[]
                                                     {
                                                         "Foo2", "Bar2",
                                                         "Baz2", "Bam2"
                                                     }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotTryPublishEventsVarArgWhenBatchSizeIsNegative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            var translator = new VarArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(
                                                     translator, 1, -1, new object[] { "Foo0", "Bar0", "Baz0", "Bam0" },
                                                     new object[] { "Foo1", "Bar1", "Baz1", "Bam1" }, new object[]
                                                     {
                                                         "Foo2", "Bar2",
                                                         "Baz2", "Bam2"
                                                     }));
            }
            finally
            {
                AssertEmptyRingBuffer(ringBuffer);
            }
        }

        [Test]
        public void ShouldNotTryPublishEventsVarArgWhenBatchStartsAtIsNegative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(() => new object[1], 4);
            var translator = new VarArgEventTranslator();

            try
            {
                Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(
                                                     translator, -1, 2, new object[] { "Foo0", "Bar0", "Baz0", "Bam0" },
                                                     new object[] { "Foo1", "Bar1", "Baz1", "Bam1" }, new object[]
                                                     {
                                                         "Foo2", "Bar2",
                                                         "Baz2", "Bam2"
                                                     }));
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
            Assert.IsTrue(ringBuffer.RemoveGatingSequence(sequenceThree));
            Assert.That(ringBuffer.GetMinimumGatingSequence(), Is.EqualTo(7L));
        }

        [Test]
        public void ShouldHandleResetToAndNotWrapUnnecessarilySingleProducer()
        {
            AssertHandleResetAndNotWrap(RingBuffer<StubEvent>.CreateSingleProducer(StubEvent.EventFactory, 4));
        }

        [Test]
        public void ShouldHandleResetToAndNotWrapUnnecessarilyMultiProducer()
        {
            AssertHandleResetAndNotWrap(RingBuffer<StubEvent>.CreateMultiProducer(StubEvent.EventFactory, 4));
        }

        private static void AssertHandleResetAndNotWrap(RingBuffer<StubEvent> rb)
        {
            var sequence = new Sequence();
            rb.AddGatingSequences(sequence);

            for (var i = 0; i < 128; i++)
            {
                rb.Publish(rb.Next());
                sequence.IncrementAndGet();
            }

            Assert.That(rb.Cursor, Is.EqualTo(127L));

            rb.ResetTo(31);
            sequence.SetValue(31);

            for (var i = 0; i < 4; i++)
            {
                rb.Publish(rb.Next());
            }

            Assert.That(rb.HasAvailableCapacity(1), Is.EqualTo(false));
        }

        private static void AssertEmptyRingBuffer(RingBuffer<object[]> ringBuffer)
        {
            Assert.That(ringBuffer[0][0], Is.EqualTo(null));
            Assert.That(ringBuffer[1][0], Is.EqualTo(null));
            Assert.That(ringBuffer[2][0], Is.EqualTo(null));
            Assert.That(ringBuffer[3][0], Is.EqualTo(null));
        }

        private class NoArgEventTranslator : IEventTranslator<object[]>
        {
            public void TranslateTo(object[] eventData, long sequence)
            {
                eventData[0] = sequence;
            }
        }

        private class VarArgEventTranslator : IEventTranslatorVararg<object[]>
        {
            public void TranslateTo(object[] eventData, long sequence, params object[] args)
            {
                eventData[0] = (string)args[0] + args[1] + args[2] + args[3] + "-" + sequence;
            }
        }

        private class ThreeArgEventTranslator : IEventTranslatorThreeArg<object[], string, string, string>
        {
            public void TranslateTo(object[] eventData, long sequence, string arg0, string arg1, string arg2)
            {
                eventData[0] = arg0 + arg1 + arg2 + "-" + sequence;
            }
        }

        private class TwoArgEventTranslator : IEventTranslatorTwoArg<object[], string, string>
        {
            public void TranslateTo(object[] eventData, long sequence, string arg0, string arg1)
            {
                eventData[0] = arg0 + arg1 + "-" + sequence;
            }
        }

        private class OneArgEventTranslator : IEventTranslatorOneArg<object[], string>
        {
            public void TranslateTo(object[] eventData, long sequence, string arg0)
            {
                eventData[0] = arg0 + "-" + sequence;
            }
        }

        private class TestEventProcessor : IEventProcessor
        {
            private readonly ISequenceBarrier _sequenceBarrier;

            public TestEventProcessor(ISequenceBarrier sequenceBarrier)
            {
                _sequenceBarrier = sequenceBarrier;
            }

            public ISequence Sequence { get; } = new Sequence();

            public void Halt()
            {
                IsRunning = false;
            }

            public void Run()
            {
                IsRunning = true;
                _sequenceBarrier.WaitFor(0L);
                Sequence.SetValue(Sequence.Value + 1);
            }

            public bool IsRunning { get; private set; }
        }

        private class TestWaiter
        {
            private readonly Barrier _barrier;
            private readonly ISequenceBarrier _sequenceBarrier;
            private readonly long _initialSequence;
            private readonly long _toWaitForSequence;
            private readonly RingBuffer<StubEvent> _ringBuffer;

            public TestWaiter(Barrier barrier, ISequenceBarrier sequenceBarrier, RingBuffer<StubEvent> ringBuffer, long initialSequence, long toWaitForSequence)
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
                _sequenceBarrier.WaitFor(_toWaitForSequence);

                var events = new List<StubEvent>();
                for (var l = _initialSequence; l <= _toWaitForSequence; l++)
                {
                    events.Add(_ringBuffer[l]);
                }

                return events;
            }
        }
    }
}
