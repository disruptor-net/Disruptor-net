using System;
using System.CodeDom.Compiler;
using System.Threading;
using Disruptor.Dsl;
using Disruptor.Tests.Support;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using Disruptor.Tests.Dsl.Stubs;

namespace Disruptor.Tests.Dsl
{
    [TestFixture]
    public class DisruptorTest
    {
        private static int TIMEOUT_IN_SECONDS = 2;
        private Disruptor<TestEvent> disruptor;
        private StubExecutor executor;
        private List<DelayedEventHandler> delayedEventHandlers = new List<DelayedEventHandler>();
        private List<TestWorkHandler> testWorkHandlers = new List<TestWorkHandler>();
        private RingBuffer<TestEvent> ringBuffer;
        private TestEvent lastPublishedEvent;

        [SetUp]
        public void setUp()
        {
            createDisruptor();
        }

        [TearDown]
        public void tearDown()
        {
            foreach (DelayedEventHandler delayedEventHandler in delayedEventHandlers)
            {
                delayedEventHandler.StopWaiting();
            }
            foreach (TestWorkHandler testWorkHandler in testWorkHandlers)
            {
                testWorkHandler.StopWaiting();
            }

            disruptor.Halt();
            executor.JoinAllThreads();
        }

        [Test]
        public void shouldCreateEventProcessorGroupForFirstEventProcessors()

        {
            executor.IgnoreExecutions();
            IEventHandler<TestEvent> eventHandler1 = new SleepingEventHandler();
            IEventHandler<TestEvent> eventHandler2 = new SleepingEventHandler();

            EventHandlerGroup<TestEvent> eventHandlerGroup =
                disruptor.HandleEventsWith(eventHandler1, eventHandler2);
            disruptor.Start();

            Assert.IsNotNull(eventHandlerGroup);
            Assert.That(executor.GetExecutionCount(), Is.EqualTo(2));
        }

        [Test]
        public void shouldMakeEntriesAvailableToFirstHandlersImmediately()
        {
            CountdownEvent countDownLatch = new CountdownEvent(2);
            IEventHandler<TestEvent> eventHandler = new EventHandlerStub<TestEvent>(countDownLatch);

            disruptor.HandleEventsWith(createDelayedEventHandler(), eventHandler);

            ensureTwoEventsProcessedAccordingToDependencies(countDownLatch);
        }

        [Test]
        public void shouldWaitUntilAllFirstEventProcessorsProcessEventBeforeMakingItAvailableToDependentEventProcessors()

        {
            DelayedEventHandler eventHandler1 = createDelayedEventHandler();

            CountdownEvent countDownLatch = new CountdownEvent(2);
            IEventHandler<TestEvent> eventHandler2 = new EventHandlerStub<TestEvent>(countDownLatch);

            disruptor.HandleEventsWith(eventHandler1).Then(eventHandler2);

            ensureTwoEventsProcessedAccordingToDependencies(countDownLatch, eventHandler1);
        }

        [Test]
        public void shouldAllowSpecifyingSpecificEventProcessorsToWaitFor()

        {
            DelayedEventHandler handler1 = createDelayedEventHandler();
            DelayedEventHandler handler2 = createDelayedEventHandler();

            CountdownEvent countDownLatch = new CountdownEvent(2);
            IEventHandler<TestEvent> handlerWithBarrier = new EventHandlerStub<TestEvent>(countDownLatch);

            disruptor.HandleEventsWith(handler1, handler2);
            disruptor.After(handler1, handler2).HandleEventsWith(handlerWithBarrier);

            ensureTwoEventsProcessedAccordingToDependencies(countDownLatch, handler1, handler2);
        }

        [Test]
        public void shouldWaitOnAllProducersJoinedByAnd()

        {
            DelayedEventHandler handler1 = createDelayedEventHandler();
            DelayedEventHandler handler2 = createDelayedEventHandler();

            CountdownEvent countDownLatch = new CountdownEvent(2);
            IEventHandler<TestEvent> handlerWithBarrier = new EventHandlerStub<TestEvent>(countDownLatch);

            disruptor.HandleEventsWith(handler1);
            EventHandlerGroup<TestEvent> handler2Group = disruptor.HandleEventsWith(handler2);
            disruptor.After(handler1).And(handler2Group).HandleEventsWith(handlerWithBarrier);

            ensureTwoEventsProcessedAccordingToDependencies(countDownLatch, handler1, handler2);
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void shouldThrowExceptionIfHandlerIsNotAlreadyConsuming()

        {
            disruptor.After(createDelayedEventHandler()).HandleEventsWith(createDelayedEventHandler());
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void shouldTrackEventHandlersByIdentityNotEquality()

        {
            EvilEqualsEventHandler handler1 = new EvilEqualsEventHandler();
            EvilEqualsEventHandler handler2 = new EvilEqualsEventHandler();

            disruptor.HandleEventsWith(handler1);

            // handler2.equals(handler1) but it hasn't yet been registered so should throw exception.
            disruptor.After(handler2);
        }

        [Test]
        public void shouldSupportSpecifyingAExceptionHandlerForEventProcessors()

        {
            Volatile.Reference<Exception> eventHandled = new Volatile.Reference<Exception>();
            IExceptionHandler<object> exceptionHandler = new StubExceptionHandler(eventHandled);
            Exception testException = new Exception();
            ExceptionThrowingEventHandler handler = new ExceptionThrowingEventHandler(testException);

            disruptor.HandleExceptionsWith(exceptionHandler);
            disruptor.HandleEventsWith(handler);

            publishEvent();

            Exception actualException = waitFor(eventHandled);
            Assert.AreSame(testException, actualException);
        }

        [Test]
        public void shouldOnlyApplyExceptionsHandlersSpecifiedViaHandleExceptionsWithOnNewEventProcessors()

        {
            Volatile.Reference<Exception> eventHandled = new Volatile.Reference<Exception>();
            IExceptionHandler<object> exceptionHandler = new StubExceptionHandler(eventHandled);
            Exception testException = new Exception();
            ExceptionThrowingEventHandler handler = new ExceptionThrowingEventHandler(testException);

            disruptor.HandleExceptionsWith(exceptionHandler);
            disruptor.HandleEventsWith(handler);
            disruptor.HandleExceptionsWith(new FatalExceptionHandler());

            publishEvent();

            Exception actualException = waitFor(eventHandled);
            Assert.AreSame(testException, actualException);
        }

        [Test]
        public void shouldSupportSpecifyingADefaultExceptionHandlerForEventProcessors()

        {
            Volatile.Reference<Exception> eventHandled = new Volatile.Reference<Exception>();
            IExceptionHandler<object> exceptionHandler = new StubExceptionHandler(eventHandled);
            Exception testException = new Exception();
            ExceptionThrowingEventHandler handler = new ExceptionThrowingEventHandler(testException);

            disruptor.SetDefaultExceptionHandler(exceptionHandler);
            disruptor.HandleEventsWith(handler);

            publishEvent();

            Exception actualException = waitFor(eventHandled);
            Assert.AreSame(testException, actualException);
        }

        [Test]
        public void shouldApplyDefaultExceptionHandlerToExistingEventProcessors()

        {
            Volatile.Reference<Exception> eventHandled = new Volatile.Reference<Exception>();
            IExceptionHandler<object> exceptionHandler = new StubExceptionHandler(eventHandled);
            Exception testException = new Exception();
            ExceptionThrowingEventHandler handler = new ExceptionThrowingEventHandler(testException);

            disruptor.HandleEventsWith(handler);
            disruptor.SetDefaultExceptionHandler(exceptionHandler);

            publishEvent();

            Exception actualException = waitFor(eventHandled);
            Assert.AreSame(testException, actualException);
        }

        [Test]
        public void shouldBlockProducerUntilAllEventProcessorsHaveAdvanced()

        {
            DelayedEventHandler delayedEventHandler = createDelayedEventHandler();
            disruptor.HandleEventsWith(delayedEventHandler);

            RingBuffer<TestEvent> ringBuffer = disruptor.Start();
            delayedEventHandler.AwaitStart();

            StubPublisher stubPublisher = new StubPublisher(ringBuffer);
            try
            {
                executor.Execute(() => stubPublisher.Run());

                assertProducerReaches(stubPublisher, 4, true);

                delayedEventHandler.ProcessEvent();
                delayedEventHandler.ProcessEvent();
                delayedEventHandler.ProcessEvent();
                delayedEventHandler.ProcessEvent();
                delayedEventHandler.ProcessEvent();

                assertProducerReaches(stubPublisher, 5, false);
            }
            finally
            {
                stubPublisher.Halt();
            }
        }

        [Test]
        public void shouldBeAbleToOverrideTheExceptionHandlerForAEventProcessor()

        {
            Exception testException = new Exception();
            ExceptionThrowingEventHandler eventHandler = new ExceptionThrowingEventHandler(testException);
            disruptor.HandleEventsWith(eventHandler);

            Volatile.Reference<Exception> reference = new Volatile.Reference<Exception>();
            StubExceptionHandler exceptionHandler = new StubExceptionHandler(reference);
            disruptor.HandleExceptionsFor(eventHandler).With(exceptionHandler);

            publishEvent();

            waitFor(reference);
        }

        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void shouldThrowExceptionWhenAddingEventProcessorsAfterTheProducerBarrierHasBeenCreated()

        {
            executor.IgnoreExecutions();
            disruptor.HandleEventsWith(new SleepingEventHandler());
            disruptor.Start();
            disruptor.HandleEventsWith(new SleepingEventHandler());
        }

        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void shouldThrowExceptionIfStartIsCalledTwice()

        {
            executor.IgnoreExecutions();
            disruptor.HandleEventsWith(new SleepingEventHandler());
            disruptor.Start();
            disruptor.Start();
        }

        [Test]
        public void shouldSupportCustomProcessorsAsDependencies()

        {
            RingBuffer<TestEvent> ringBuffer = disruptor.RingBuffer;

            DelayedEventHandler delayedEventHandler = createDelayedEventHandler();

            CountdownEvent countDownLatch = new CountdownEvent(2);
            IEventHandler<TestEvent> handlerWithBarrier = new EventHandlerStub<TestEvent>(countDownLatch);

            BatchEventProcessor<TestEvent> processor =
                new BatchEventProcessor<TestEvent>(ringBuffer, ringBuffer.NewBarrier(), delayedEventHandler);
            disruptor.HandleEventsWith(processor);
            disruptor.After(processor).HandleEventsWith(handlerWithBarrier);

            ensureTwoEventsProcessedAccordingToDependencies(countDownLatch, delayedEventHandler);
        }

        [Test]
        public void shouldSupportHandlersAsDependenciesToCustomProcessors()

        {
            DelayedEventHandler delayedEventHandler = createDelayedEventHandler();
            disruptor.HandleEventsWith(delayedEventHandler);


            RingBuffer<TestEvent> ringBuffer = disruptor.RingBuffer;
            CountdownEvent countDownLatch = new CountdownEvent(2);
            IEventHandler<TestEvent> handlerWithBarrier = new EventHandlerStub<TestEvent>(countDownLatch);

            ISequenceBarrier sequenceBarrier = disruptor.After(delayedEventHandler).AsSequenceBarrier();
            BatchEventProcessor<TestEvent> processor =
                new BatchEventProcessor<TestEvent>(ringBuffer, sequenceBarrier, handlerWithBarrier);
            disruptor.HandleEventsWith(processor);

            ensureTwoEventsProcessedAccordingToDependencies(countDownLatch, delayedEventHandler);
        }

        [Test]
        public void shouldSupportCustomProcessorsAndHandlersAsDependencies()
        {
            DelayedEventHandler delayedEventHandler1 = createDelayedEventHandler();
            DelayedEventHandler delayedEventHandler2 = createDelayedEventHandler();
            disruptor.HandleEventsWith(delayedEventHandler1);


            RingBuffer<TestEvent> ringBuffer = disruptor.RingBuffer;
            CountdownEvent countDownLatch = new CountdownEvent(2);
            IEventHandler<TestEvent> handlerWithBarrier = new EventHandlerStub<TestEvent>(countDownLatch);

            ISequenceBarrier sequenceBarrier = disruptor.After(delayedEventHandler1).AsSequenceBarrier();
            BatchEventProcessor<TestEvent> processor =
                new BatchEventProcessor<TestEvent>(ringBuffer, sequenceBarrier, delayedEventHandler2);

            disruptor.After(delayedEventHandler1).And(processor).HandleEventsWith(handlerWithBarrier);

            ensureTwoEventsProcessedAccordingToDependencies(countDownLatch, delayedEventHandler1, delayedEventHandler2);
        }

        [Test]
        public void shouldProvideEventsToWorkHandlers()
        {
            TestWorkHandler workHandler1 = createTestWorkHandler();
            TestWorkHandler workHandler2 = createTestWorkHandler();
            disruptor.HandleEventsWithWorkerPool(workHandler1, workHandler2);

            publishEvent();
            publishEvent();

            workHandler1.ProcessEvent();
            workHandler2.ProcessEvent();
        }

        [Test]
        public void shouldSupportUsingWorkerPoolAsDependency()
        {
            TestWorkHandler workHandler1 = createTestWorkHandler();
            TestWorkHandler workHandler2 = createTestWorkHandler();
            DelayedEventHandler delayedEventHandler = createDelayedEventHandler();
            disruptor.HandleEventsWithWorkerPool(workHandler1, workHandler2).Then(delayedEventHandler);

            publishEvent();
            publishEvent();

            Assert.That(disruptor.GetBarrierFor(delayedEventHandler).Cursor, Is.EqualTo(-1L));

            workHandler2.ProcessEvent();
            workHandler1.ProcessEvent();

            delayedEventHandler.ProcessEvent();
        }

        [Test]
        public void shouldSupportUsingWorkerPoolAsDependencyAndProcessFirstEventAsSoonAsItIsAvailable()
        {
            TestWorkHandler workHandler1 = createTestWorkHandler();
            TestWorkHandler workHandler2 = createTestWorkHandler();
            DelayedEventHandler delayedEventHandler = createDelayedEventHandler();
            disruptor.HandleEventsWithWorkerPool(workHandler1, workHandler2).Then(delayedEventHandler);

            publishEvent();
            publishEvent();

            workHandler1.ProcessEvent();
            delayedEventHandler.ProcessEvent();

            workHandler2.ProcessEvent();
            delayedEventHandler.ProcessEvent();
        }

        [Test]
        public void shouldSupportUsingWorkerPoolWithADependency()
        {
            TestWorkHandler workHandler1 = createTestWorkHandler();
            TestWorkHandler workHandler2 = createTestWorkHandler();
            DelayedEventHandler delayedEventHandler = createDelayedEventHandler();
            disruptor.HandleEventsWith(delayedEventHandler).ThenHandleEventsWithWorkerPool(workHandler1, workHandler2);

            publishEvent();
            publishEvent();

            delayedEventHandler.ProcessEvent();
            delayedEventHandler.ProcessEvent();

            workHandler1.ProcessEvent();
            workHandler2.ProcessEvent();
        }

        [Test]
        public void shouldSupportCombiningWorkerPoolWithEventHandlerAsDependencyWhenNotPreviouslyRegistered()

        {
            TestWorkHandler workHandler1 = createTestWorkHandler();
            DelayedEventHandler delayedEventHandler1 = createDelayedEventHandler();
            DelayedEventHandler delayedEventHandler2 = createDelayedEventHandler();
            disruptor.HandleEventsWith(delayedEventHandler1).And(disruptor.HandleEventsWithWorkerPool(workHandler1)).Then(
                    delayedEventHandler2);

            publishEvent();
            publishEvent();

            delayedEventHandler1.ProcessEvent();
            delayedEventHandler1.ProcessEvent();

            workHandler1.ProcessEvent();
            delayedEventHandler2.ProcessEvent();

            workHandler1.ProcessEvent();
            delayedEventHandler2.ProcessEvent();
        }

        [Test]
        [ExpectedException(typeof(TimeoutException))]
        public void shouldThrowTimeoutExceptionIfShutdownDoesNotCompleteNormally()
        {
            //Given
            DelayedEventHandler delayedEventHandler = createDelayedEventHandler();
            disruptor.HandleEventsWith(delayedEventHandler);
            publishEvent();

            //When
            disruptor.Shutdown(TimeSpan.FromSeconds(1));

            //Then
        }

        [Test]
        public void shouldTrackRemainingCapacity()
        {
            long[] remainingCapacity = { -1 };
            //Given
            IEventHandler<TestEvent> eventHandler = new TempEventHandler(disruptor, remainingCapacity);

            disruptor.HandleEventsWith(eventHandler);

            //When
            publishEvent();

            //Then
            while (remainingCapacity[0] == -1)
            {
                Thread.Sleep(100);
            }
            Assert.That(remainingCapacity[0], Is.EqualTo(ringBuffer.BufferSize - 1L));
            Assert.That(disruptor.RingBuffer.GetRemainingCapacity(), Is.EqualTo(ringBuffer.BufferSize - 0L));
        }

        private class TempEventHandler : IEventHandler<TestEvent>
        {
            private readonly Disruptor<TestEvent> _disruptor;
            private readonly long[] _remainingCapacity;

            public TempEventHandler(Disruptor<TestEvent> disruptor, long[] remainingCapacity)
            {
                _disruptor = disruptor;
                _remainingCapacity = remainingCapacity;
            }

            public void OnEvent(TestEvent data, long sequence, bool endOfBatch)
            {
                _remainingCapacity[0] = _disruptor.RingBuffer.GetRemainingCapacity();
            }
        }

        [Test]
        public void shouldAllowEventHandlerWithSuperType()
        {
            CountdownEvent latch = new CountdownEvent(2);
            IEventHandler<Object> objectHandler = new EventHandlerStub<Object>(latch);

            disruptor.HandleEventsWith(objectHandler);

            ensureTwoEventsProcessedAccordingToDependencies(latch);
        }

        [Test]
        public void shouldAllowChainingEventHandlersWithSuperType()
        {
            CountdownEvent latch = new CountdownEvent(2);
            DelayedEventHandler delayedEventHandler = createDelayedEventHandler();
            IEventHandler<Object> objectHandler = new EventHandlerStub<Object>(latch);

            disruptor.HandleEventsWith(delayedEventHandler).Then(objectHandler);

            ensureTwoEventsProcessedAccordingToDependencies(latch, delayedEventHandler);
        }

        [Test]
        public void shouldMakeEntriesAvailableToFirstCustomProcessorsImmediately()
        {
            CountdownEvent countDownLatch = new CountdownEvent(2);
            IEventHandler<TestEvent> eventHandler = new EventHandlerStub<TestEvent>(countDownLatch);

            disruptor.HandleEventsWith(new EventProcessorFactory(disruptor, eventHandler, 0));

            ensureTwoEventsProcessedAccordingToDependencies(countDownLatch);
        }

        public class EventProcessorFactory : IEventProcessorFactory<TestEvent>
        {
            private readonly Disruptor<TestEvent> _disruptor;
            private readonly IEventHandler<TestEvent> _eventHandler;
            private readonly int _sequenceLength;

            public EventProcessorFactory(Disruptor<TestEvent> disruptor, IEventHandler<TestEvent> eventHandler, int sequenceLength)
            {
                _disruptor = disruptor;
                _eventHandler = eventHandler;
                _sequenceLength = sequenceLength;
            }

            public IEventProcessor CreateEventProcessor(RingBuffer<TestEvent> ringBuffer, Sequence[] barrierSequences)
            {
                Assert.AreEqual(_sequenceLength, barrierSequences.Length, "Should not have had any barrier sequences");
                return new BatchEventProcessor<TestEvent>(_disruptor.RingBuffer, ringBuffer.NewBarrier(barrierSequences), _eventHandler);
            }
        }

        [Test]
        public void shouldHonourDependenciesForCustomProcessors()
        {
            CountdownEvent countDownLatch = new CountdownEvent(2);
            IEventHandler<TestEvent> eventHandler = new EventHandlerStub<TestEvent>(countDownLatch);
            DelayedEventHandler delayedEventHandler = createDelayedEventHandler();

            disruptor.HandleEventsWith(delayedEventHandler).Then(new EventProcessorFactory(disruptor, eventHandler, 0));

            ensureTwoEventsProcessedAccordingToDependencies(countDownLatch, delayedEventHandler);
        }

        private TestWorkHandler createTestWorkHandler()
        {
            TestWorkHandler testWorkHandler = new TestWorkHandler();
            testWorkHandlers.Add(testWorkHandler);
            return testWorkHandler;
        }

        private void ensureTwoEventsProcessedAccordingToDependencies(
            CountdownEvent countDownLatch,
            params DelayedEventHandler[] dependencies)
        {
            publishEvent();
            publishEvent();

            foreach (DelayedEventHandler dependency in dependencies)
            {
                assertThatCountDownLatchEquals(countDownLatch, 2L);
                dependency.ProcessEvent();
                dependency.ProcessEvent();
            }

            assertThatCountDownLatchIsZero(countDownLatch);
        }

        private void assertProducerReaches(
            StubPublisher stubPublisher,
            int expectedPublicationCount,
            bool strict)
        {
            long loopStart = DateTime.UtcNow.Ticks;
            while (stubPublisher.GetPublicationCount() < expectedPublicationCount && DateTime.UtcNow.Ticks - loopStart < 5000)
            {
                Thread.Yield();
            }

            if (strict)
            {
                Assert.That(stubPublisher.GetPublicationCount(), Is.EqualTo(expectedPublicationCount));
            }
            else
            {
                int actualPublicationCount = stubPublisher.GetPublicationCount();
                Assert.IsTrue(actualPublicationCount >= expectedPublicationCount, "Producer reached unexpected count. Expected at least " + expectedPublicationCount + " but only reached " + actualPublicationCount);
            }
        }

        private void createDisruptor()
        {
            executor = new StubExecutor();
            createDisruptor(executor);
        }

        private void createDisruptor(TaskScheduler executor)
        {
            disruptor = new Disruptor<TestEvent>(() => new TestEvent(), 4, executor, ProducerType.Single, new BlockingWaitStrategy());
        }

        private TestEvent publishEvent()
        {
            if (ringBuffer == null)
            {
                ringBuffer = disruptor.Start();

                foreach (DelayedEventHandler eventHandler in delayedEventHandlers)
                {
                    eventHandler.AwaitStart();
                }
            }

            disruptor.PublishEvent(new EventTranslator(this));

            return lastPublishedEvent;
        }

        private class EventTranslator : IEventTranslator<TestEvent>
        {
            private readonly DisruptorTest _disruptorTest;

            public EventTranslator(DisruptorTest disruptorTest)
            {
                _disruptorTest = disruptorTest;
            }

            public void TranslateTo(TestEvent eventData, long sequence)
            {
                _disruptorTest.lastPublishedEvent = eventData;
            }
        }

        private Exception waitFor(Volatile.Reference<Exception> reference)
        {
            while (reference.ReadFullFence() == null)
            {
                Thread.Yield();
            }

            return reference.ReadFullFence();
        }

        private DelayedEventHandler createDelayedEventHandler()
        {
            DelayedEventHandler delayedEventHandler = new DelayedEventHandler();
            delayedEventHandlers.Add(delayedEventHandler);
            return delayedEventHandler;
        }

        private void assertThatCountDownLatchEquals(
            CountdownEvent countDownLatch,
            long expectedCountDownValue)
        {
            Assert.That(countDownLatch.CurrentCount, Is.EqualTo(expectedCountDownValue));
        }

        private void assertThatCountDownLatchIsZero(CountdownEvent countDownLatch)
        {
            bool released = countDownLatch.Wait(TimeSpan.FromSeconds(TIMEOUT_IN_SECONDS));
            Assert.IsTrue(released, "Batch handler did not receive entries: " + countDownLatch.CurrentCount);
        }
    }
}
