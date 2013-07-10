using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Tests.Support;
using Disruptor.Dsl;
using NUnit.Framework;

namespace Disruptor.Tests.Dsl
{
    [TestFixture]
    public class DisruptorTests
    {
        private const int TIMEOUT_IN_SECONDS = 2;
        private StubTaskScheduler _scheduler;
        private Disruptor<TestEvent> _disruptor;        
        private List<DelayedEventHandler> _delayedEventHandlers = new List<DelayedEventHandler>();
        private RingBuffer<TestEvent> _ringBuffer;
        private TestEvent _lastPublishedEvent;

      [SetUp]
      public void SetUp()
      {
          CreateDisruptor();
      }

      [TearDown]
      public void TearDown()
      {
          foreach (var delayedEventHandler in _delayedEventHandlers)
          {
              delayedEventHandler.StopWaiting();
          }

          _disruptor.Halt();
          _ringBuffer = null;

          _scheduler.WaitAllTasks();
      }

      [Test]
      public void ShouldCreateEventProcessorGroupForFirstEventProcessors()
      {
          _scheduler.IgnoreNewTasks = true;
          IEventHandler<TestEvent> eventHandler1 = new SleepingEventHandler();
          IEventHandler<TestEvent> eventHandler2 = new SleepingEventHandler();

          EventHandlerGroup<TestEvent> eventHandlerGroup = _disruptor.HandleEventsWith(eventHandler1, eventHandler2);
          _disruptor.Start();

          Assert.NotNull(eventHandlerGroup);
          Assert.AreEqual(2, _scheduler.ExecutionCount);
      }


        [Test]
        public void ShouldMakeEntriesAvailableToFirstHandlersImmediately()
        {
            var  countDownEvent = new CountdownEvent(2);
            IEventHandler<TestEvent> eventHandler = new EventHandlerStub(countDownEvent);

            _disruptor.HandleEventsWith(CreateDelayedEventHandler(), eventHandler);

            EnsureTwoEventsProcessedAccordingToDependencies(countDownEvent);
        }

        [Test]
        public void ShouldWaitUntilAllFirstEventProcessorsProcessEventBeforeMakingItAvailableToDependentEventProcessors()
        {
            var eventHandler1 = CreateDelayedEventHandler();

            var countDownEvent = new CountdownEvent(2);
            IEventHandler<TestEvent> eventHandler2 = new EventHandlerStub(countDownEvent);

            _disruptor.HandleEventsWith(eventHandler1).Then(eventHandler2);

            EnsureTwoEventsProcessedAccordingToDependencies(countDownEvent, eventHandler1);
        }

        [Test]
        public void ShouldAllowSpecifyingSpecificEventProcessorsToWaitFor()
        {
            var handler1 = CreateDelayedEventHandler();
            var handler2 = CreateDelayedEventHandler();

            var countDownEvent = new CountdownEvent(2);
            IEventHandler<TestEvent> handlerWithBarrier = new EventHandlerStub(countDownEvent);

            _disruptor.HandleEventsWith(handler1, handler2);
            _disruptor.After(handler1, handler2).HandleEventsWith(handlerWithBarrier);

            EnsureTwoEventsProcessedAccordingToDependencies(countDownEvent, handler1, handler2);
        }


        [Test]
        public void ShouldWaitOnAllProducersJoinedByAnd()
        {
            var handler1 = CreateDelayedEventHandler();
            var handler2 = CreateDelayedEventHandler();

            var countDownEvent = new CountdownEvent(2);
            IEventHandler<TestEvent> handlerWithBarrier = new EventHandlerStub(countDownEvent);

            _disruptor.HandleEventsWith(handler1, handler2);
            _disruptor.After(handler1).And(handler2).HandleEventsWith(handlerWithBarrier);

            EnsureTwoEventsProcessedAccordingToDependencies(countDownEvent, handler1, handler2);
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void ShouldThrowExceptionIfHandlerIsNotAlreadyConsuming()     
        {
            _disruptor.After(CreateDelayedEventHandler()).HandleEventsWith(CreateDelayedEventHandler());
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void ShouldThrowExceptionIfHandlerUsedWithAndIsNotAlreadyConsuming()        
        {
            var handler1 = CreateDelayedEventHandler();
            var handler2 = CreateDelayedEventHandler();
            _disruptor.HandleEventsWith(handler1);
            _disruptor.After(handler1).And(handler2);
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void shouldTrackEventHandlersByIdentityNotEquality()    
        {
            var handler1 = new EvilEqualsEventHandler();
            var handler2 = new EvilEqualsEventHandler();

            _disruptor.HandleEventsWith(handler1);

            // handler2.equals(handler1) but it hasn't yet been registered so should throw exception.
            _disruptor.After(handler2);
        }



#if HELL_FROZE_OVER
    @Test
    public void shouldSupportSpecifyingADefaultExceptionHandlerForEventProcessors()
        throws Exception
    {
        AtomicReference<Throwable> eventHandled = new AtomicReference<Throwable>();
        ExceptionHandler exceptionHandler = new StubExceptionHandler(eventHandled);
        RuntimeException testException = new RuntimeException();
        ExceptionThrowingEventHandler handler = new ExceptionThrowingEventHandler(testException);

        disruptor.handleExceptionsWith(exceptionHandler);
        disruptor.handleEventsWith(handler);

        publishEvent();

        final Throwable actualException = waitFor(eventHandled);
        assertSame(testException, actualException);
    }

    @Test
    public void shouldBlockProducerUntilAllEventProcessorsHaveAdvanced()
        throws Exception
    {
        final DelayedEventHandler delayedEventHandler = createDelayedEventHandler();
        disruptor.handleEventsWith(delayedEventHandler);

        final RingBuffer<TestEvent> ringBuffer = disruptor.start();

        final StubPublisher stubPublisher = new StubPublisher(ringBuffer);
        try
        {
            executor.execute(stubPublisher);

            assertProducerReaches(stubPublisher, 4, true);

            delayedEventHandler.processEvent();
            delayedEventHandler.processEvent();
            delayedEventHandler.processEvent();
            delayedEventHandler.processEvent();
            delayedEventHandler.processEvent();

            assertProducerReaches(stubPublisher, 5, false);
        }
        finally
        {
            stubPublisher.halt();
        }
    }

    @Test
    public void shouldBeAbleToOverrideTheExceptionHandlerForAEventProcessor()
        throws Exception
    {
        final RuntimeException testException = new RuntimeException();
        final ExceptionThrowingEventHandler eventHandler = new ExceptionThrowingEventHandler(testException);
        disruptor.handleEventsWith(eventHandler);

        AtomicReference<Throwable> reference = new AtomicReference<Throwable>();
        StubExceptionHandler exceptionHandler = new StubExceptionHandler(reference);
        disruptor.handleExceptionsFor(eventHandler).with(exceptionHandler);

        publishEvent();

        waitFor(reference);
    }

    @Test(expected = IllegalStateException.class)
    public void shouldThrowExceptionWhenAddingEventProcessorsAfterTheProducerBarrierHasBeenCreated()
        throws Exception
    {
        executor.ignoreExecutions();
        disruptor.handleEventsWith(new SleepingEventHandler());
        disruptor.start();
        disruptor.handleEventsWith(new SleepingEventHandler());
    }

    @Test(expected = IllegalStateException.class)
    public void shouldThrowExceptionIfStartIsCalledTwice()
        throws Exception
    {
        executor.ignoreExecutions();
        disruptor.handleEventsWith(new SleepingEventHandler());
        disruptor.start();
        disruptor.start();
    }

    @Test
    public void shouldSupportCustomProcessorsAsDependencies()
        throws Exception
    {
        RingBuffer<TestEvent> ringBuffer = disruptor.getRingBuffer();

        final DelayedEventHandler delayedEventHandler = createDelayedEventHandler();

        CountDownLatch countDownLatch = new CountDownLatch(2);
        EventHandler<TestEvent> handlerWithBarrier = new EventHandlerStub(countDownLatch);

        final BatchEventProcessor<TestEvent> processor =
            new BatchEventProcessor<TestEvent>(ringBuffer, ringBuffer.newBarrier(), delayedEventHandler);
        disruptor.handleEventsWith(processor);
        disruptor.after(processor).handleEventsWith(handlerWithBarrier);

        ensureTwoEventsProcessedAccordingToDependencies(countDownLatch, delayedEventHandler);
    }

    @Test
    public void shouldSupportHandlersAsDependenciesToCustomProcessors()
        throws Exception
    {
        final DelayedEventHandler delayedEventHandler = createDelayedEventHandler();
        disruptor.handleEventsWith(delayedEventHandler);


        RingBuffer<TestEvent> ringBuffer = disruptor.getRingBuffer();
        CountDownLatch countDownLatch = new CountDownLatch(2);
        EventHandler<TestEvent> handlerWithBarrier = new EventHandlerStub(countDownLatch);

        final SequenceBarrier sequenceBarrier = disruptor.after(delayedEventHandler).asSequenceBarrier();
        final BatchEventProcessor<TestEvent> processor =
            new BatchEventProcessor<TestEvent>(ringBuffer, sequenceBarrier, handlerWithBarrier);
        disruptor.handleEventsWith(processor);

        ensureTwoEventsProcessedAccordingToDependencies(countDownLatch, delayedEventHandler);
    }

    @Test
    public void shouldSupportCustomProcessorsAndHandlersAsDependencies()
        throws Exception
    {
        final DelayedEventHandler delayedEventHandler1 = createDelayedEventHandler();
        final DelayedEventHandler delayedEventHandler2 = createDelayedEventHandler();
        disruptor.handleEventsWith(delayedEventHandler1);


        RingBuffer<TestEvent> ringBuffer = disruptor.getRingBuffer();
        CountDownLatch countDownLatch = new CountDownLatch(2);
        EventHandler<TestEvent> handlerWithBarrier = new EventHandlerStub(countDownLatch);

        final SequenceBarrier sequenceBarrier = disruptor.after(delayedEventHandler1).asSequenceBarrier();
        final BatchEventProcessor<TestEvent> processor =
            new BatchEventProcessor<TestEvent>(ringBuffer, sequenceBarrier, delayedEventHandler2);

        disruptor.after(delayedEventHandler1).and(processor).handleEventsWith(handlerWithBarrier);

        ensureTwoEventsProcessedAccordingToDependencies(countDownLatch, delayedEventHandler1, delayedEventHandler2);
    }

#endif

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



      private void CreateDisruptor()
      {
          _scheduler = new StubTaskScheduler();
          CreateDisruptor(_scheduler);
      }


      private void CreateDisruptor(TaskScheduler scheduler)
      {
        _disruptor = new Disruptor<TestEvent>(() => new TestEvent(),
                                             new SingleThreadedClaimStrategy(4),
                                             new BlockingWaitStrategy(), 
                                             scheduler);
      }

      private TestEvent PublishEvent()
      {
        if (_ringBuffer == null)
        {
            _ringBuffer = _disruptor.Start();
        }

          _disruptor.PublishEvent((@event, l) => { _lastPublishedEvent = @event; return @event; });      

        return _lastPublishedEvent;
    }

    private DelayedEventHandler CreateDelayedEventHandler()
    {
        var delayedEventHandler = new DelayedEventHandler();
        _delayedEventHandlers.Add(delayedEventHandler);
        return delayedEventHandler;
    }

    private void AssertThatCountDownLatchEquals(CountdownEvent countDownEvent, long expectedCountDownValue)
    {
        Assert.AreEqual(expectedCountDownValue, countDownEvent.CurrentCount);
    }

    private void AssertThatCountDownLatchIsZero(CountdownEvent countDownEvent)
    {
        Assert.IsTrue(countDownEvent.Wait(TIMEOUT_IN_SECONDS * 1000), "Batch handler did not receive entries.");
    }
    }
}
