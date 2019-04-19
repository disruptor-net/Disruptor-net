using System;
using System.Collections.Generic;
using System.Threading;
using Disruptor.Dsl;
using Disruptor.Tests.Dsl.Stubs;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests.Dsl
{
    [TestFixture]
    public class ValueDisruptorTests
    {
        private const int _timeoutInSeconds = 2;
        private ValueDisruptor<TestValueEvent> _disruptor;
        private StubExecutor _executor;
        private List<DelayedEventHandler> _delayedEventHandlers;
        private ValueRingBuffer<TestValueEvent> _ringBuffer;

        [SetUp]
        public void SetUp()
        {
            _ringBuffer = null;
            _delayedEventHandlers = new List<DelayedEventHandler>();
            _executor = new StubExecutor();
            _disruptor = new ValueDisruptor<TestValueEvent>(() => new TestValueEvent(), 4, _executor);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var delayedEventHandler in _delayedEventHandlers)
            {
                delayedEventHandler.StopWaiting();
            }

            _disruptor.Halt();
            _executor.JoinAllThreads();
        }

        [Test]
        public void ShouldProcessMessagesPublishedBeforeStartIsCalled()
        {
            var eventCounter = new CountdownEvent(2);
            _disruptor.HandleEventsWith(new TestValueEventHandler<TestValueEvent>(e => eventCounter.Signal()));

            _disruptor.PublishEvent().Dispose();

            _disruptor.Start();

            _disruptor.PublishEvent().Dispose();

            if (!eventCounter.Wait(TimeSpan.FromSeconds(5)))
                Assert.Fail("Did not process event published before start was called. Missed events: " + eventCounter.CurrentCount);
        }

        [Test]
        public void ShouldAddEventProcessorsAfterPublishing()
        {
            var rb = _disruptor.RingBuffer;
            var b1 = BatchEventProcessorFactory.Create(rb, rb.NewBarrier(), new SleepingEventHandler());
            var b2 = BatchEventProcessorFactory.Create(rb, rb.NewBarrier(b1.Sequence), new SleepingEventHandler());
            var b3 = BatchEventProcessorFactory.Create(rb, rb.NewBarrier(b2.Sequence), new SleepingEventHandler());

            Assert.That(b1.Sequence.Value, Is.EqualTo(-1L));
            Assert.That(b2.Sequence.Value, Is.EqualTo(-1L));
            Assert.That(b3.Sequence.Value, Is.EqualTo(-1L));

            rb.Publish(rb.Next());
            rb.Publish(rb.Next());
            rb.Publish(rb.Next());
            rb.Publish(rb.Next());
            rb.Publish(rb.Next());
            rb.Publish(rb.Next());

            _disruptor.HandleEventsWith(b1, b2, b3);

            Assert.That(b1.Sequence.Value, Is.EqualTo(5L));
            Assert.That(b2.Sequence.Value, Is.EqualTo(5L));
            Assert.That(b3.Sequence.Value, Is.EqualTo(5L));
        }

        [Test]
        public void ShouldSetSequenceForHandlerIfAddedAfterPublish()
        {
            var rb = _disruptor.RingBuffer;
            var b1 = new SleepingEventHandler();
            var b2 = new SleepingEventHandler();
            var b3 = new SleepingEventHandler();

            rb.Publish(rb.Next());
            rb.Publish(rb.Next());
            rb.Publish(rb.Next());
            rb.Publish(rb.Next());
            rb.Publish(rb.Next());
            rb.Publish(rb.Next());

            _disruptor.HandleEventsWith(b1, b2, b3);

            Assert.That(_disruptor.GetSequenceValueFor(b1), Is.EqualTo(5L));
            Assert.That(_disruptor.GetSequenceValueFor(b2), Is.EqualTo(5L));
            Assert.That(_disruptor.GetSequenceValueFor(b3), Is.EqualTo(5L));
        }

        [Test]
        public void ShouldCreateEventProcessorGroupForFirstEventProcessors()
        {
            _executor.IgnoreExecutions();
            var eventHandler1 = new SleepingEventHandler();
            var eventHandler2 = new SleepingEventHandler();

            var eventHandlerGroup =
                _disruptor.HandleEventsWith(eventHandler1, eventHandler2);
            _disruptor.Start();

            Assert.IsNotNull(eventHandlerGroup);
            Assert.That(_executor.GetExecutionCount(), Is.EqualTo(2));
        }

        [Test]
        public void ShouldMakeEntriesAvailableToFirstHandlersImmediately()
        {
            var countDownLatch = new CountdownEvent(2);
            var eventHandler = new CountDownValueEventHandler<TestValueEvent>(countDownLatch);

            _disruptor.HandleEventsWith(CreateDelayedEventHandler(), eventHandler);

            EnsureTwoEventsProcessedAccordingToDependencies(countDownLatch);
        }

        [Test]
        public void ShouldWaitUntilAllFirstEventProcessorsProcessEventBeforeMakingItAvailableToDependentEventProcessors()
        {
            var eventHandler1 = CreateDelayedEventHandler();

            var countDownLatch = new CountdownEvent(2);
            var eventHandler2 = new CountDownValueEventHandler<TestValueEvent>(countDownLatch);

            _disruptor.HandleEventsWith(eventHandler1).Then(eventHandler2);

            EnsureTwoEventsProcessedAccordingToDependencies(countDownLatch, eventHandler1);
        }

        [Test]
        public void ShouldAllowSpecifyingSpecificEventProcessorsToWaitFor()
        {
            var handler1 = CreateDelayedEventHandler();
            var handler2 = CreateDelayedEventHandler();

            var countDownLatch = new CountdownEvent(2);
            var handlerWithBarrier = new CountDownValueEventHandler<TestValueEvent>(countDownLatch);

            _disruptor.HandleEventsWith(handler1, handler2);
            _disruptor.After(handler1, handler2).HandleEventsWith(handlerWithBarrier);

            EnsureTwoEventsProcessedAccordingToDependencies(countDownLatch, handler1, handler2);
        }

        [Test]
        public void ShouldWaitOnAllProducersJoinedByAnd()

        {
            var handler1 = CreateDelayedEventHandler();
            var handler2 = CreateDelayedEventHandler();

            var countDownLatch = new CountdownEvent(2);
            var handlerWithBarrier = new CountDownValueEventHandler<TestValueEvent>(countDownLatch);

            _disruptor.HandleEventsWith(handler1);
            var handler2Group = _disruptor.HandleEventsWith(handler2);
            _disruptor.After(handler1).And(handler2Group).HandleEventsWith(handlerWithBarrier);

            EnsureTwoEventsProcessedAccordingToDependencies(countDownLatch, handler1, handler2);
        }

        [Test]
        public void ShouldThrowExceptionIfHandlerIsNotAlreadyConsuming()
        {
            Assert.Throws<ArgumentException>(() => _disruptor.After(CreateDelayedEventHandler()).HandleEventsWith(CreateDelayedEventHandler()));
        }

        [Test]
        public void ShouldTrackEventHandlersByIdentityNotEquality()
        {
            var handler1 = new EvilEqualsEventHandler();
            var handler2 = new EvilEqualsEventHandler();

            _disruptor.HandleEventsWith(handler1);

            // handler2.equals(handler1) but it hasn't yet been registered so should throw exception.
            Assert.Throws<ArgumentException>(() => _disruptor.After(handler2));
        }

        [Test]
        public void ShouldSupportSpecifyingADefaultExceptionHandlerForEventProcessors()
        {
            var eventHandled = new AtomicReference<Exception>();
            var exceptionHandler = new StubExceptionHandler(eventHandled);
            var testException = new Exception();
            var handler = new ExceptionThrowingEventHandler(testException);

            _disruptor.SetDefaultExceptionHandler(exceptionHandler);
            _disruptor.HandleEventsWith(handler);

            PublishEvent();

            var actualException = WaitFor(eventHandled);
            Assert.AreSame(testException, actualException);
        }

        [Test]
        public void ShouldApplyDefaultExceptionHandlerToExistingEventProcessors()
        {
            var eventHandled = new AtomicReference<Exception>();
            var exceptionHandler = new StubExceptionHandler(eventHandled);
            var testException = new Exception();
            var handler = new ExceptionThrowingEventHandler(testException);

            _disruptor.HandleEventsWith(handler);
            _disruptor.SetDefaultExceptionHandler(exceptionHandler);

            PublishEvent();

            var actualException = WaitFor(eventHandled);
            Assert.AreSame(testException, actualException);
        }

        [Test]
        public void ShouldBlockProducerUntilAllEventProcessorsHaveAdvanced()
        {
            var delayedEventHandler = CreateDelayedEventHandler();
            _disruptor.HandleEventsWith(delayedEventHandler);

            var ringBuffer = _disruptor.Start();
            delayedEventHandler.AwaitStart();

            var stubPublisher = new StubPublisher(ringBuffer);
            try
            {
                _executor.Execute(() => stubPublisher.Run());

                AssertProducerReaches(stubPublisher, 4, true);

                delayedEventHandler.ProcessEvent();
                delayedEventHandler.ProcessEvent();
                delayedEventHandler.ProcessEvent();
                delayedEventHandler.ProcessEvent();
                delayedEventHandler.ProcessEvent();

                AssertProducerReaches(stubPublisher, 5, false);
            }
            finally
            {
                stubPublisher.Halt();
            }
        }

        [Test]
        public void ShouldBeAbleToOverrideTheExceptionHandlerForAEventProcessor()
        {
            var testException = new Exception();
            var eventHandler = new ExceptionThrowingEventHandler(testException);
            _disruptor.HandleEventsWith(eventHandler);

            var reference = new AtomicReference<Exception>();
            var exceptionHandler = new StubExceptionHandler(reference);
            _disruptor.HandleExceptionsFor(eventHandler).With(exceptionHandler);

            PublishEvent();

            WaitFor(reference);
        }

        [Test]
        public void ShouldThrowExceptionWhenAddingEventProcessorsAfterTheProducerBarrierHasBeenCreated()
        {
            _executor.IgnoreExecutions();
            _disruptor.HandleEventsWith(new SleepingEventHandler());
            _disruptor.Start();

            Assert.Throws<InvalidOperationException>(() => _disruptor.HandleEventsWith(new SleepingEventHandler()));
        }

        [Test]
        public void ShouldThrowExceptionIfStartIsCalledTwice()
        {
            _executor.IgnoreExecutions();
            _disruptor.HandleEventsWith(new SleepingEventHandler());
            _disruptor.Start();

            Assert.Throws<InvalidOperationException>(() => _disruptor.Start());
        }

        [Test]
        public void ShouldSupportCustomProcessorsAsDependencies()
        {
            var ringBuffer = _disruptor.RingBuffer;

            var delayedEventHandler = CreateDelayedEventHandler();

            var countDownLatch = new CountdownEvent(2);
            var handlerWithBarrier = new CountDownValueEventHandler<TestValueEvent>(countDownLatch);

            var processor = BatchEventProcessorFactory.Create(ringBuffer, ringBuffer.NewBarrier(), delayedEventHandler);
            _disruptor.HandleEventsWith(processor).Then(handlerWithBarrier);

            EnsureTwoEventsProcessedAccordingToDependencies(countDownLatch, delayedEventHandler);
        }

        [Test]
        public void ShouldSupportHandlersAsDependenciesToCustomProcessors()
        {
            var delayedEventHandler = CreateDelayedEventHandler();
            _disruptor.HandleEventsWith(delayedEventHandler);

            var ringBuffer = _disruptor.RingBuffer;
            var countDownLatch = new CountdownEvent(2);
            var handlerWithBarrier = new CountDownValueEventHandler<TestValueEvent>(countDownLatch);

            var sequenceBarrier = _disruptor.After(delayedEventHandler).AsSequenceBarrier();
            var processor = BatchEventProcessorFactory.Create(ringBuffer, sequenceBarrier, handlerWithBarrier);
            _disruptor.HandleEventsWith(processor);

            EnsureTwoEventsProcessedAccordingToDependencies(countDownLatch, delayedEventHandler);
        }

        [Test]
        public void ShouldSupportCustomProcessorsAndHandlersAsDependencies()
        {
            var delayedEventHandler1 = CreateDelayedEventHandler();
            var delayedEventHandler2 = CreateDelayedEventHandler();
            _disruptor.HandleEventsWith(delayedEventHandler1);

            var ringBuffer = _disruptor.RingBuffer;
            var countDownLatch = new CountdownEvent(2);
            var handlerWithBarrier = new CountDownValueEventHandler<TestValueEvent>(countDownLatch);

            var sequenceBarrier = _disruptor.After(delayedEventHandler1).AsSequenceBarrier();
            var processor = BatchEventProcessorFactory.Create(ringBuffer, sequenceBarrier, delayedEventHandler2);

            _disruptor.After(delayedEventHandler1).And(processor).HandleEventsWith(handlerWithBarrier);

            EnsureTwoEventsProcessedAccordingToDependencies(countDownLatch, delayedEventHandler1, delayedEventHandler2);
        }

        [Test]
        public void ShouldThrowTimeoutExceptionIfShutdownDoesNotCompleteNormally()
        {
            //Given
            var delayedEventHandler = CreateDelayedEventHandler();
            _disruptor.HandleEventsWith(delayedEventHandler);
            PublishEvent();

            //When
            Assert.Throws<TimeoutException>(() => _disruptor.Shutdown(TimeSpan.FromSeconds(1)));
        }

        [Test]
        public void ShouldTrackRemainingCapacity()
        {
            long[] remainingCapacity = { -1 };
            //Given
            var eventHandler = new TempEventHandler(_disruptor, remainingCapacity);

            _disruptor.HandleEventsWith(eventHandler);

            //When
            PublishEvent();

            //Then
            while (remainingCapacity[0] == -1)
            {
                Thread.Sleep(100);
            }
            Assert.That(remainingCapacity[0], Is.EqualTo(_ringBuffer.BufferSize - 1L));
            Assert.That(_disruptor.RingBuffer.GetRemainingCapacity(), Is.EqualTo(_ringBuffer.BufferSize - 0L));
        }

        private class TempEventHandler : IValueEventHandler<TestValueEvent>
        {
            private readonly ValueDisruptor<TestValueEvent> _disruptor;
            private readonly long[] _remainingCapacity;

            public TempEventHandler(ValueDisruptor<TestValueEvent> disruptor, long[] remainingCapacity)
            {
                _disruptor = disruptor;
                _remainingCapacity = remainingCapacity;
            }

            public void OnEvent(ref TestValueEvent data, long sequence, bool endOfBatch)
            {
                _remainingCapacity[0] = _disruptor.RingBuffer.GetRemainingCapacity();
            }
        }

        [Test]
        public void ShouldAllowEventHandlerWithSuperType()
        {
            var latch = new CountdownEvent(2);
            var objectHandler = new CountDownValueEventHandler<TestValueEvent>(latch);

            _disruptor.HandleEventsWith(objectHandler);

            EnsureTwoEventsProcessedAccordingToDependencies(latch);
        }

        [Test]
        public void ShouldAllowChainingEventHandlersWithSuperType()
        {
            var latch = new CountdownEvent(2);
            var delayedEventHandler = CreateDelayedEventHandler();
            var objectHandler = new CountDownValueEventHandler<TestValueEvent>(latch);

            _disruptor.HandleEventsWith(delayedEventHandler).Then(objectHandler);

            EnsureTwoEventsProcessedAccordingToDependencies(latch, delayedEventHandler);
        }

        [Test]
        public void ShouldMakeEntriesAvailableToFirstCustomProcessorsImmediately()
        {
            var countDownLatch = new CountdownEvent(2);
            var eventHandler = new CountDownValueEventHandler<TestValueEvent>(countDownLatch);

            _disruptor.HandleEventsWith(new EventProcessorFactory(_disruptor, eventHandler, 0));

            EnsureTwoEventsProcessedAccordingToDependencies(countDownLatch);
        }

        private class EventProcessorFactory : IValueEventProcessorFactory<TestValueEvent>
        {
            private readonly ValueDisruptor<TestValueEvent> _disruptor;
            private readonly IValueEventHandler<TestValueEvent> _eventHandler;
            private readonly int _sequenceLength;

            public EventProcessorFactory(ValueDisruptor<TestValueEvent> disruptor, IValueEventHandler<TestValueEvent> eventHandler, int sequenceLength)
            {
                _disruptor = disruptor;
                _eventHandler = eventHandler;
                _sequenceLength = sequenceLength;
            }

            public IEventProcessor CreateEventProcessor(ValueRingBuffer<TestValueEvent> ringBuffer, ISequence[] barrierSequences)
            {
                Assert.AreEqual(_sequenceLength, barrierSequences.Length, "Should not have had any barrier sequences");
                return BatchEventProcessorFactory.Create(_disruptor.RingBuffer, ringBuffer.NewBarrier(barrierSequences), _eventHandler);
            }
        }

        [Test]
        public void ShouldxHonourDependenciesForCustomProcessors()
        {
            var countDownLatch = new CountdownEvent(2);
            var eventHandler = new CountDownValueEventHandler<TestValueEvent>(countDownLatch);
            var delayedEventHandler = CreateDelayedEventHandler();

            _disruptor.HandleEventsWith(delayedEventHandler).Then(new EventProcessorFactory(_disruptor, eventHandler, 1));

            EnsureTwoEventsProcessedAccordingToDependencies(countDownLatch, delayedEventHandler);
        }

        private void EnsureTwoEventsProcessedAccordingToDependencies(CountdownEvent countDownLatch, params DelayedEventHandler[] dependencies)
        {
            PublishEvent();
            PublishEvent();

            foreach (var dependency in dependencies)
            {
                AssertThatCountDownLatchEquals(countDownLatch, 2L);
                dependency.ProcessEvent();
                dependency.ProcessEvent();
            }

            AssertThatCountDownLatchIsZero(countDownLatch);
        }

        private static void AssertProducerReaches(StubPublisher stubPublisher, int expectedPublicationCount, bool strict)
        {
            var loopStart = DateTime.UtcNow;
            while (stubPublisher.GetPublicationCount() < expectedPublicationCount && DateTime.UtcNow - loopStart < TimeSpan.FromMilliseconds(5))
            {
                Thread.Yield();
            }

            if (strict)
            {
                Assert.That(stubPublisher.GetPublicationCount(), Is.EqualTo(expectedPublicationCount));
            }
            else
            {
                var actualPublicationCount = stubPublisher.GetPublicationCount();
                Assert.IsTrue(actualPublicationCount >= expectedPublicationCount, "Producer reached unexpected count. Expected at least " + expectedPublicationCount + " but only reached " + actualPublicationCount);
            }
        }

        private void PublishEvent()
        {
            if (_ringBuffer == null)
            {
                _ringBuffer = _disruptor.Start();

                foreach (var eventHandler in _delayedEventHandlers)
                {
                    eventHandler.AwaitStart();
                }
            }

            _disruptor.PublishEvent().Dispose();
        }

        private static Exception WaitFor(AtomicReference<Exception> reference)
        {
            while (reference.Read() == null)
            {
                Thread.Yield();
            }

            return reference.Read();
        }

        private DelayedEventHandler CreateDelayedEventHandler()
        {
            var delayedEventHandler = new DelayedEventHandler();
            _delayedEventHandlers.Add(delayedEventHandler);
            return delayedEventHandler;
        }

        private void AssertThatCountDownLatchEquals(CountdownEvent countDownLatch, long expectedCountDownValue)
        {
            Assert.That(countDownLatch.CurrentCount, Is.EqualTo(expectedCountDownValue));
        }

        private void AssertThatCountDownLatchIsZero(CountdownEvent countDownLatch)
        {
            var released = countDownLatch.Wait(TimeSpan.FromSeconds(_timeoutInSeconds));
            Assert.IsTrue(released, "Batch handler did not receive entries: " + countDownLatch.CurrentCount);
        }
    }
}
