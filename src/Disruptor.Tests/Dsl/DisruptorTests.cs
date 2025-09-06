﻿using System;
using System.Collections.Generic;
using System.Threading;
using Disruptor.Dsl;
using Disruptor.Processing;
using Disruptor.Tests.Dsl.Stubs;
using Disruptor.Tests.Support;
using NUnit.Framework;

#pragma warning disable 618

namespace Disruptor.Tests.Dsl;

[TestFixture]
public class DisruptorTests : IDisposable
{
    private const int _timeoutInSeconds = 2;
    private readonly Disruptor<TestEvent> _disruptor;
    private readonly StubTaskScheduler _taskScheduler = new();
    private readonly List<DelayedEventHandler> _delayedEventHandlers = new();
    private readonly List<TestWorkHandler> _testWorkHandlers = new();

    public DisruptorTests()
    {
        _disruptor = new Disruptor<TestEvent>(() => new TestEvent(), 4, _taskScheduler, ProducerType.Multi, new AsyncWaitStrategy());
    }

    public void Dispose()
    {
        foreach (var delayedEventHandler in _delayedEventHandlers)
        {
            delayedEventHandler.StopWaiting();
        }
        foreach (var testWorkHandler in _testWorkHandlers)
        {
            testWorkHandler.StopWaiting();
        }

        _disruptor.Dispose();

        Assert.That(_taskScheduler.JoinAllThreads(1000));
    }

    [Test]
    public void ShouldHaveStartedAfterStartCalled()
    {
        Assert.That(!_disruptor.HasStarted, "Should only be set to started after Start is called");

        _disruptor.Start();

        Assert.That(_disruptor.HasStarted, "Should be set to started after Start is called");

        _disruptor.Halt();

        Assert.That(_disruptor.HasStarted, "Should be still be started after Halt is called");
    }

    [Test]
    public void ShouldBeRunningAfterStartCalled()
    {
        Assert.That(!_disruptor.IsRunning, "Should only be set to running after Start is called");

        _disruptor.Start();

        Assert.That(_disruptor.IsRunning, "Should be set to running after Start is called");

        _disruptor.Halt();

        Assert.That(!_disruptor.IsRunning, "Should no longer be running after Halt is called");
    }

    [Test]
    public void ShouldPublishAndHandleEvent_EventHandler()
    {
        var eventCounter = new CountdownEvent(2);
        var values = new List<int>();

        _disruptor.HandleEventsWith(new TestEventHandler<TestEvent>(e => values.Add(e.Value)))
                  .Then(new TestEventHandler<TestEvent>(e => eventCounter.Signal()));

        _disruptor.Start();

        using (var scope = _disruptor.PublishEvent())
        {
            scope.Event().Value = 101;
        }
        using (var scope = _disruptor.PublishEvent())
        {
            scope.Event().Value = 102;
        }

        Assert.That(eventCounter.Wait(TimeSpan.FromSeconds(5)));
        Assert.That(values, Is.EqualTo(new List<int> { 101, 102 }));
    }

    [Test]
    public void ShouldPublishAndHandleEvent_BatchEventHandler()
    {
        var eventCounter = new CountdownEvent(2);
        var values = new List<int>();

        _disruptor.HandleEventsWith(new TestBatchEventHandler<TestEvent>(e => values.Add(e.Value)))
                  .Then(new TestBatchEventHandler<TestEvent>(e => eventCounter.Signal()));

        _disruptor.Start();

        using (var scope = _disruptor.PublishEvent())
        {
            scope.Event().Value = 101;
        }
        using (var scope = _disruptor.PublishEvent())
        {
            scope.Event().Value = 102;
        }

        Assert.That(eventCounter.Wait(TimeSpan.FromSeconds(5)));
        Assert.That(values, Is.EqualTo(new List<int> { 101, 102 }));
    }

    [Test]
    public void ShouldPublishAndHandleEvent_AsyncBatchEventHandler()
    {
        var eventCounter = new CountdownEvent(2);
        var values = new List<int>();

        _disruptor.HandleEventsWith(new TestBatchEventHandler<TestEvent>(e => values.Add(e.Value)))
                  .Then(new TestBatchEventHandler<TestEvent>(e => eventCounter.Signal()));

        _disruptor.Start();

        using (var scope = _disruptor.PublishEvent())
        {
            scope.Event().Value = 101;
        }
        using (var scope = _disruptor.PublishEvent())
        {
            scope.Event().Value = 102;
        }

        Assert.That(eventCounter.Wait(TimeSpan.FromSeconds(5)));
        Assert.That(values, Is.EqualTo(new List<int> { 101, 102 }));
    }

    [Test]
    public void ShouldPublishAndHandleEvents_EventHandler()
    {
        var eventCounter = new CountdownEvent(4);
        var values = new List<int>();

        _disruptor.HandleEventsWith(new TestEventHandler<TestEvent>(e => values.Add(e.Value)))
                  .Then(new TestEventHandler<TestEvent>(e => eventCounter.Signal()));

        _disruptor.Start();

        using (var scope = _disruptor.PublishEvents(2))
        {
            scope.Event(0).Value = 101;
            scope.Event(1).Value = 102;
        }
        using (var scope = _disruptor.PublishEvents(2))
        {
            scope.Event(0).Value = 103;
            scope.Event(1).Value = 104;
        }

        Assert.That(eventCounter.Wait(TimeSpan.FromSeconds(5)));
        Assert.That(values, Is.EqualTo(new List<int> { 101, 102, 103, 104 }));
    }

    [Test]
    public void ShouldPublishAndHandleEvents_BatchEventHandler()
    {
        var eventCounter = new CountdownEvent(4);
        var values = new List<int>();

        _disruptor.HandleEventsWith(new TestBatchEventHandler<TestEvent>(e => values.Add(e.Value)))
                  .Then(new TestBatchEventHandler<TestEvent>(e => eventCounter.Signal()));

        _disruptor.Start();

        using (var scope = _disruptor.PublishEvents(2))
        {
            scope.Event(0).Value = 101;
            scope.Event(1).Value = 102;
        }
        using (var scope = _disruptor.PublishEvents(2))
        {
            scope.Event(0).Value = 103;
            scope.Event(1).Value = 104;
        }

        Assert.That(eventCounter.Wait(TimeSpan.FromSeconds(5)));
        Assert.That(values, Is.EqualTo(new List<int> { 101, 102, 103, 104 }));
    }

    [Test]
    public void ShouldPublishAndHandleEvents_AsyncBatchEventHandler()
    {
        var eventCounter = new CountdownEvent(4);
        var values = new List<int>();

        _disruptor.HandleEventsWith(new TestAsyncBatchEventHandler<TestEvent>(e => values.Add(e.Value)))
                  .Then(new TestAsyncBatchEventHandler<TestEvent>(e => eventCounter.Signal()));

        _disruptor.Start();

        using (var scope = _disruptor.PublishEvents(2))
        {
            scope.Event(0).Value = 101;
            scope.Event(1).Value = 102;
        }
        using (var scope = _disruptor.PublishEvents(2))
        {
            scope.Event(0).Value = 103;
            scope.Event(1).Value = 104;
        }

        Assert.That(eventCounter.Wait(TimeSpan.FromSeconds(5)));
        Assert.That(values, Is.EqualTo(new List<int> { 101, 102, 103, 104 }));
    }

    [Test]
    public void ShouldProcessMessagesPublishedBeforeStartIsCalled()
    {
        var eventCounter = new CountdownEvent(2);
        _disruptor.HandleEventsWith(new TestEventHandler<TestEvent>(e => eventCounter.Signal()));

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
        var b1 = EventProcessorFactory.Create(rb, rb.NewBarrier(), new SleepingEventHandler());
        var b2 = EventProcessorFactory.Create(rb, rb.NewBarrier(b1.Sequence), new SleepingEventHandler());
        var b3 = EventProcessorFactory.Create(rb, rb.NewBarrier(b2.Sequence), new SleepingEventHandler());

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
    public void ShouldSetSequenceForWorkProcessorIfAddedAfterPublish()
    {
        var rb = _disruptor.RingBuffer;
        var wh1 = CreateTestWorkHandler();
        var wh2 = CreateTestWorkHandler();
        var wh3 = CreateTestWorkHandler();

        rb.Publish(rb.Next());
        rb.Publish(rb.Next());
        rb.Publish(rb.Next());
        rb.Publish(rb.Next());
        rb.Publish(rb.Next());
        rb.Publish(rb.Next());

        _disruptor.HandleEventsWithWorkerPool(wh1, wh2, wh3);

        Assert.That(_disruptor.RingBuffer.GetMinimumGatingSequence(), Is.EqualTo(5L));
    }

    [Test]
    public void ShouldGetDependentSequencesForHandler()
    {
        var rb = _disruptor.RingBuffer;
        var handler = new TestEventHandler<TestEvent>();

        _disruptor.HandleEventsWith(handler);
        _disruptor.Start();

        rb.Publish(rb.Next());
        rb.Publish(rb.Next());
        rb.Publish(rb.Next());
        rb.Publish(rb.Next());
        rb.Publish(rb.Next());
        rb.Publish(rb.Next());

        var sequenceGroup = _disruptor.GetDependentSequencesFor(handler);
        Assert.That(sequenceGroup, Is.Not.Null);
        Assert.That(sequenceGroup!.CursorValue, Is.EqualTo(5L));
        Assert.That(sequenceGroup!.DependsOnCursor, Is.True);
        Assert.That(sequenceGroup!.DependentSequenceCount, Is.EqualTo(1));
    }

    [Test]
    public void ShouldGetDependentSequencesForHandlerIfAddedAfterPublish()
    {
        var rb = _disruptor.RingBuffer;
        var handler = new TestEventHandler<TestEvent>();

        rb.Publish(rb.Next());
        rb.Publish(rb.Next());
        rb.Publish(rb.Next());
        rb.Publish(rb.Next());
        rb.Publish(rb.Next());
        rb.Publish(rb.Next());

        _disruptor.HandleEventsWith(handler);
        _disruptor.Start();

        var sequenceGroup = _disruptor.GetDependentSequencesFor(handler);
        Assert.That(sequenceGroup, Is.Not.Null);
        Assert.That(sequenceGroup!.CursorValue, Is.EqualTo(5L));
        Assert.That(sequenceGroup!.DependsOnCursor, Is.True);
        Assert.That(sequenceGroup!.DependentSequenceCount, Is.EqualTo(1));
    }

    [Test]
    public void ShouldGetDependentSequencesForMultipleHandlers()
    {
        var h1 = new TestEventHandler<TestEvent>();
        var h2 = new TestEventHandler<TestEvent>();
        var h3 = new TestEventHandler<TestEvent>();

        _disruptor.HandleEventsWith(h1).Then(h2, h3);

        var sequenceGroup1 = _disruptor.GetDependentSequencesFor(h1)!;
        var sequenceGroup2 = _disruptor.GetDependentSequencesFor(h2)!;
        var sequenceGroup3 = _disruptor.GetDependentSequencesFor(h3)!;

        Assert.That(sequenceGroup1.HasSameDependencies(sequenceGroup2), Is.False);
        Assert.That(sequenceGroup2.HasSameDependencies(sequenceGroup3), Is.True);
        Assert.That(sequenceGroup1.DependsOnCursor, Is.True); ;
        Assert.That(sequenceGroup2.DependsOnCursor, Is.False);
        Assert.That(sequenceGroup3.DependsOnCursor, Is.False);
    }

    [Test]
    public void ShouldConfigureDependentSequencesTags()
    {
        var h1 = new TestEventHandler<TestEvent>();
        var h2 = new TestEventHandler<TestEvent>();
        var h3 = new TestEventHandler<TestEvent>();

        _disruptor.HandleEventsWith(h1).Then(h2, h3);

        _disruptor.GetDependentSequencesFor(h1)!.Tag = "1";
        _disruptor.GetDependentSequencesFor(h2)!.Tag = "2";
        _disruptor.GetDependentSequencesFor(h3)!.Tag = "3";

        Assert.That(_disruptor.GetDependentSequencesFor(h1)!.Tag, Is.EqualTo("1"));
        Assert.That(_disruptor.GetDependentSequencesFor(h2)!.Tag, Is.EqualTo("2"));
        Assert.That(_disruptor.GetDependentSequencesFor(h3)!.Tag, Is.EqualTo("3"));
    }

    [Test]
    public void ShouldCreateEventProcessorGroupForFirstEventProcessors()
    {
        using var _ = _taskScheduler.SuspendExecutions();

        var eventHandler1 = new SleepingEventHandler();
        var eventHandler2 = new SleepingEventHandler();

        var eventHandlerGroup = _disruptor.HandleEventsWith(eventHandler1, eventHandler2);
        _disruptor.Start();

        Assert.That(eventHandlerGroup, Is.Not.Null);
        Assert.That(_taskScheduler.TaskCount, Is.EqualTo(2));
    }

    [Test]
    public void ShouldMakeEntriesAvailableToFirstHandlersImmediately()
    {
        var countDownLatch = new CountdownEvent(2);
        var eventHandler = new CountDownEventHandler<TestEvent>(countDownLatch);

        _disruptor.HandleEventsWith(CreateDelayedEventHandler(), eventHandler);

        EnsureTwoEventsProcessedAccordingToDependencies(countDownLatch);
    }

    [Test]
    public void ShouldWaitUntilAllFirstEventProcessorsProcessEventBeforeMakingItAvailableToDependentEventProcessors()
    {
        var eventHandler1 = CreateDelayedEventHandler();

        var countDownLatch = new CountdownEvent(2);
        var eventHandler2 = new CountDownEventHandler<TestEvent>(countDownLatch);

        _disruptor.HandleEventsWith(eventHandler1).Then(eventHandler2);

        EnsureTwoEventsProcessedAccordingToDependencies(countDownLatch, eventHandler1);
    }

    [Test]
    public void ShouldSupportAddingCustomEventProcessorWithFactory()
    {
        var rb = _disruptor.RingBuffer;
        var b1 = EventProcessorFactory.Create(rb, rb.NewBarrier(), new SleepingEventHandler());
        var b2 = new EventProcessorCreator<TestEvent>((ringBuffer, sequenceBarrier) => EventProcessorFactory.Create(ringBuffer, sequenceBarrier, new SleepingEventHandler()));

        _disruptor.HandleEventsWith(b1).Then(b2);

        _disruptor.Start();

        Assert.That(_taskScheduler.TaskCount, Is.EqualTo(2));
    }

    [Test]
    public void ShouldAllowSpecifyingSpecificEventProcessorsToWaitFor()
    {
        var handler1 = CreateDelayedEventHandler();
        var handler2 = CreateDelayedEventHandler();

        var countDownLatch = new CountdownEvent(2);
        var handlerWithBarrier = new CountDownEventHandler<TestEvent>(countDownLatch);

        _disruptor.HandleEventsWith(handler1, handler2);
        _disruptor.After(handler1, handler2).HandleEventsWith(handlerWithBarrier);

        EnsureTwoEventsProcessedAccordingToDependencies(countDownLatch, handler1, handler2);

        Assert.That(_taskScheduler.TaskCount, Is.EqualTo(3));
    }

    [Test]
    public void ShouldWaitOnAllProducersJoinedByAnd()

    {
        var handler1 = CreateDelayedEventHandler();
        var handler2 = CreateDelayedEventHandler();

        var countDownLatch = new CountdownEvent(2);
        var handlerWithBarrier = new CountDownEventHandler<TestEvent>(countDownLatch);

        _disruptor.HandleEventsWith(handler1);
        var handler2Group = _disruptor.HandleEventsWith(handler2);
        _disruptor.After(handler1).And(handler2Group).HandleEventsWith(handlerWithBarrier);

        EnsureTwoEventsProcessedAccordingToDependencies(countDownLatch, handler1, handler2);

        Assert.That(_taskScheduler.TaskCount, Is.EqualTo(3));
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
    public void ShouldSupportSpecifyingADefaultExceptionHandlerForEventProcessors_EventHandler()
    {
        var eventHandled = new AtomicReference<Exception>();
        var exceptionHandler = new StubExceptionHandler(eventHandled);
        var testException = new Exception();
        var handler = new ExceptionThrowingEventHandler(testException);

        _disruptor.SetDefaultExceptionHandler(exceptionHandler);
        _disruptor.HandleEventsWith(handler);

        PublishEvent();

        var actualException = WaitFor(eventHandled);
        Assert.That(actualException, Is.SameAs(testException));
    }

    [Test]
    public void ShouldSupportSpecifyingADefaultExceptionHandlerForEventProcessors_BatchEventHandler()
    {
        var eventHandled = new AtomicReference<Exception>();
        var exceptionHandler = new StubExceptionHandler(eventHandled);
        var testException = new Exception();
        var handler = new TestBatchEventHandler<TestEvent>(_ => throw testException);

        _disruptor.SetDefaultExceptionHandler(exceptionHandler);
        _disruptor.HandleEventsWith(handler);

        PublishEvent();

        var actualException = WaitFor(eventHandled);
        Assert.That(actualException, Is.SameAs(testException));
    }

    [Test]
    public void ShouldSupportSpecifyingADefaultExceptionHandlerForEventProcessors_AsyncBatchEventHandler()
    {
        var eventHandled = new AtomicReference<Exception>();
        var exceptionHandler = new StubExceptionHandler(eventHandled);
        var testException = new Exception();
        var handler = new TestAsyncBatchEventHandler<TestEvent>(_ => throw testException);

        _disruptor.SetDefaultExceptionHandler(exceptionHandler);
        _disruptor.HandleEventsWith(handler);

        PublishEvent();

        var actualException = WaitFor(eventHandled);
        Assert.That(actualException, Is.SameAs(testException));
    }

    [Test]
    public void ShouldApplyDefaultExceptionHandlerToExistingEventProcessors_EventHandler()
    {
        var eventHandled = new AtomicReference<Exception>();
        var exceptionHandler = new StubExceptionHandler(eventHandled);
        var testException = new Exception();
        var handler = new ExceptionThrowingEventHandler(testException);

        _disruptor.HandleEventsWith(handler);
        _disruptor.SetDefaultExceptionHandler(exceptionHandler);

        PublishEvent();

        var actualException = WaitFor(eventHandled);
        Assert.That(actualException, Is.SameAs(testException));
    }

    [Test]
    public void ShouldApplyDefaultExceptionHandlerToExistingEventProcessors_BatchEventHandler()
    {
        var eventHandled = new AtomicReference<Exception>();
        var exceptionHandler = new StubExceptionHandler(eventHandled);
        var testException = new Exception();
        var handler = new TestBatchEventHandler<TestEvent>(_ => throw testException);

        _disruptor.HandleEventsWith(handler);
        _disruptor.SetDefaultExceptionHandler(exceptionHandler);

        PublishEvent();

        var actualException = WaitFor(eventHandled);
        Assert.That(actualException, Is.SameAs(testException));
    }

    [Test]
    public void ShouldApplyDefaultExceptionHandlerToExistingEventProcessors_AsyncBatchEventHandler()
    {
        var eventHandled = new AtomicReference<Exception>();
        var exceptionHandler = new StubExceptionHandler(eventHandled);
        var testException = new Exception();
        var handler = new TestAsyncBatchEventHandler<TestEvent>(_ => throw testException);

        _disruptor.HandleEventsWith(handler);
        _disruptor.SetDefaultExceptionHandler(exceptionHandler);

        PublishEvent();

        var actualException = WaitFor(eventHandled);
        Assert.That(actualException, Is.SameAs(testException));
    }

    [Test]
    public void ShouldBlockProducerUntilAllEventProcessorsHaveAdvanced()
    {
        var delayedEventHandler = CreateDelayedEventHandler();
        _disruptor.HandleEventsWith(delayedEventHandler);
        _disruptor.Start();

        var ringBuffer = _disruptor.RingBuffer;
        delayedEventHandler.AwaitStart();

        var stubPublisher = new StubPublisher(ringBuffer);
        var publisherTask = stubPublisher.Start();
        try
        {
            stubPublisher.AssertProducerReaches(4, true);

            delayedEventHandler.ProcessEvent();
            delayedEventHandler.ProcessEvent();
            delayedEventHandler.ProcessEvent();
            delayedEventHandler.ProcessEvent();
            delayedEventHandler.ProcessEvent();

            stubPublisher.AssertProducerReaches(5, false);
        }
        finally
        {
            stubPublisher.Halt();
            while (!publisherTask.Wait(1))
            {
                delayedEventHandler.TryProcessEvent();
            }
        }
    }

    [Test]
    public void ShouldBlockProducerUntilAllEventProcessorsHaveAdvancedWithMaxBatchSize()
    {
        var delayedEventHandler = CreateDelayedEventHandler(1);
        _disruptor.HandleEventsWith(delayedEventHandler);
        _disruptor.Start();

        var ringBuffer = _disruptor.RingBuffer;
        delayedEventHandler.AwaitStart();

        var stubPublisher = new StubPublisher(ringBuffer);
        var publisherTask = stubPublisher.Start();
        try
        {
            stubPublisher.AssertProducerReaches(4, true);

            delayedEventHandler.ProcessEvent();

            stubPublisher.AssertProducerReaches(5, true);

            delayedEventHandler.ProcessEvent();
            delayedEventHandler.ProcessEvent();

            stubPublisher.AssertProducerReaches(7, true);
        }
        finally
        {
            stubPublisher.Halt();
            while (!publisherTask.Wait(1))
            {
                delayedEventHandler.TryProcessEvent();
            }
        }
    }

    [Test]
    [Obsolete("Target method is obsolete")]
    public void ShouldBeAbleToOverrideTheExceptionHandlerForAEventProcessorV1()
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
    public void ShouldBeAbleToOverrideTheExceptionHandlerForAEventProcessorV2()
    {
        var testException = new Exception();
        var eventHandler = new ExceptionThrowingEventHandler(testException);
        _disruptor.HandleEventsWith(eventHandler);

        var reference = new AtomicReference<Exception>();
        var exceptionHandler = new StubExceptionHandler(reference);
        _disruptor.SetExceptionHandler(eventHandler, exceptionHandler);

        PublishEvent();

        WaitFor(reference);
    }

    [Test]
    [Obsolete("Target method is obsolete")]
    public void ShouldBeAbleToOverrideTheExceptionHandlerForAEventProcessor_BatchHandlerV1()
    {
        var testException = new Exception();
        var eventHandler = new TestBatchEventHandler<TestEvent>(_ => throw testException);
        _disruptor.HandleEventsWith(eventHandler);

        var reference = new AtomicReference<Exception>();
        var exceptionHandler = new StubExceptionHandler(reference);
        _disruptor.HandleExceptionsFor(eventHandler).With(exceptionHandler);

        PublishEvent();

        WaitFor(reference);
    }

    [Test]
    public void ShouldBeAbleToOverrideTheExceptionHandlerForAEventProcessor_BatchHandlerV2()
    {
        var testException = new Exception();
        var eventHandler = new TestBatchEventHandler<TestEvent>(_ => throw testException);
        _disruptor.HandleEventsWith(eventHandler);

        var reference = new AtomicReference<Exception>();
        var exceptionHandler = new StubExceptionHandler(reference);
        _disruptor.SetExceptionHandler(eventHandler, exceptionHandler);

        PublishEvent();

        WaitFor(reference);
    }

    [Test]
    [Obsolete("Target method is obsolete")]
    public void ShouldBeAbleToOverrideTheExceptionHandlerForAEventProcessor_AsyncBatchHandlerV1()
    {
        var testException = new Exception();
        var eventHandler = new TestAsyncBatchEventHandler<TestEvent>(_ => throw testException);
        _disruptor.HandleEventsWith(eventHandler);

        var reference = new AtomicReference<Exception>();
        var exceptionHandler = new StubExceptionHandler(reference);
        _disruptor.HandleExceptionsFor(eventHandler).With(exceptionHandler);

        PublishEvent();

        WaitFor(reference);
    }

    [Test]
    public void ShouldBeAbleToOverrideTheExceptionHandlerForAEventProcessor_AsyncBatchHandlerV2()
    {
        var testException = new Exception();
        var eventHandler = new TestAsyncBatchEventHandler<TestEvent>(_ => throw testException);
        _disruptor.HandleEventsWith(eventHandler);

        var reference = new AtomicReference<Exception>();
        var exceptionHandler = new StubExceptionHandler(reference);
        _disruptor.SetExceptionHandler(eventHandler, exceptionHandler);

        PublishEvent();

        WaitFor(reference);
    }

    [Test]
    public void ShouldThrowExceptionWhenAddingEventProcessorsAfterTheProducerBarrierHasBeenCreated()
    {
        using var _ = _taskScheduler.SuspendExecutions();

        _disruptor.HandleEventsWith(new SleepingEventHandler());
        _disruptor.Start();

        Assert.Throws<InvalidOperationException>(() => _disruptor.HandleEventsWith(new SleepingEventHandler()));
    }

    [Test]
    public void ShouldThrowExceptionIfStartIsCalledTwice()
    {
        using var _ = _taskScheduler.SuspendExecutions();

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
        var handlerWithBarrier = new CountDownEventHandler<TestEvent>(countDownLatch);

        var processor = EventProcessorFactory.Create(ringBuffer, ringBuffer.NewBarrier(), delayedEventHandler);
        _disruptor.HandleEventsWith(processor).Then(handlerWithBarrier);

        EnsureTwoEventsProcessedAccordingToDependencies(countDownLatch, delayedEventHandler);

        Assert.That(_taskScheduler.TaskCount, Is.EqualTo(2));
    }

    [Test]
    public void ShouldSupportHandlersAsDependenciesToCustomProcessors()
    {
        var delayedEventHandler = CreateDelayedEventHandler();
        _disruptor.HandleEventsWith(delayedEventHandler);

        var ringBuffer = _disruptor.RingBuffer;
        var countDownLatch = new CountdownEvent(2);
        var handlerWithBarrier = new CountDownEventHandler<TestEvent>(countDownLatch);

        var sequenceBarrier = _disruptor.After(delayedEventHandler).AsSequenceBarrier();
        var processor = EventProcessorFactory.Create(ringBuffer, sequenceBarrier, handlerWithBarrier);
        _disruptor.HandleEventsWith(processor);

        EnsureTwoEventsProcessedAccordingToDependencies(countDownLatch, delayedEventHandler);

        Assert.That(_taskScheduler.TaskCount, Is.EqualTo(2));
    }

    [Test]
    public void ShouldSupportHandlersAsDependenciesToCustomProcessorsAsync()
    {
        var delayedEventHandler = CreateDelayedEventHandler();
        _disruptor.HandleEventsWith(delayedEventHandler);

        var ringBuffer = _disruptor.RingBuffer;
        var countDownLatch = new CountdownEvent(2);
        var handlerWithBarrier = new AsyncCountDownEventHandler<TestEvent>(countDownLatch);

        var sequenceBarrier = _disruptor.After(delayedEventHandler).AsAsyncSequenceBarrier();
        var processor = EventProcessorFactory.Create(ringBuffer, sequenceBarrier, handlerWithBarrier);
        _disruptor.HandleEventsWith(processor);

        EnsureTwoEventsProcessedAccordingToDependencies(countDownLatch, delayedEventHandler);

        Assert.That(_taskScheduler.TaskCount, Is.EqualTo(2));
    }

    [Test]
    public void ShouldSupportCustomProcessorsAndHandlersAsDependencies()
    {
        var delayedEventHandler1 = CreateDelayedEventHandler();
        var delayedEventHandler2 = CreateDelayedEventHandler();
        _disruptor.HandleEventsWith(delayedEventHandler1);

        var ringBuffer = _disruptor.RingBuffer;
        var countDownLatch = new CountdownEvent(2);
        var handlerWithBarrier = new CountDownEventHandler<TestEvent>(countDownLatch);

        var sequenceBarrier = _disruptor.After(delayedEventHandler1).AsSequenceBarrier();
        var processor = EventProcessorFactory.Create(ringBuffer, sequenceBarrier, delayedEventHandler2);

        _disruptor.After(delayedEventHandler1).And(processor).HandleEventsWith(handlerWithBarrier);

        EnsureTwoEventsProcessedAccordingToDependencies(countDownLatch, delayedEventHandler1, delayedEventHandler2);

        Assert.That(_taskScheduler.TaskCount, Is.EqualTo(3));
    }

    [Test]
    public void ShouldSupportMultipleCustomProcessorsAndHandlersAsDependencies()
    {
        var ringBuffer = _disruptor.RingBuffer;
        var countDownLatch = new CountdownEvent(2);
        var handlerWithBarrier = new CountDownEventHandler<TestEvent>(countDownLatch);

        var delayedEventHandler1 = CreateDelayedEventHandler();
        var processor1 = EventProcessorFactory.Create(ringBuffer, ringBuffer.NewBarrier(), delayedEventHandler1);

        var delayedEventHandler2 = CreateDelayedEventHandler();
        var processor2 = EventProcessorFactory.Create(ringBuffer, ringBuffer.NewBarrier(), delayedEventHandler2);

        _disruptor.HandleEventsWith(processor1, processor2);
        _disruptor.After(processor1, processor2).HandleEventsWith(handlerWithBarrier);

        EnsureTwoEventsProcessedAccordingToDependencies(countDownLatch, delayedEventHandler1, delayedEventHandler2);

        Assert.That(_taskScheduler.TaskCount, Is.EqualTo(3));
    }

    [Test]
    public void ShouldProvideEventsToWorkHandlers()
    {
        var workHandler1 = CreateTestWorkHandler();
        var workHandler2 = CreateTestWorkHandler();
        _disruptor.HandleEventsWithWorkerPool(workHandler1, workHandler2);

        PublishEvent();
        PublishEvent();

        workHandler1.ProcessEvent();
        workHandler2.ProcessEvent();
    }

    [Test]
    public void ShouldProvideEventsMultipleWorkHandlers()
    {
        var workHandler1 = CreateTestWorkHandler();
        var workHandler2 = CreateTestWorkHandler();
        var workHandler3 = CreateTestWorkHandler();
        var workHandler4 = CreateTestWorkHandler();
        var workHandler5 = CreateTestWorkHandler();
        var workHandler6 = CreateTestWorkHandler();
        var workHandler7 = CreateTestWorkHandler();
        var workHandler8 = CreateTestWorkHandler();

        _disruptor
            .HandleEventsWithWorkerPool(workHandler1, workHandler2)
            .ThenHandleEventsWithWorkerPool(workHandler3, workHandler4);
        _disruptor
            .HandleEventsWithWorkerPool(workHandler5, workHandler6)
            .ThenHandleEventsWithWorkerPool(workHandler7, workHandler8);
    }

    [Test]
    public void ShouldSupportUsingWorkerPoolAsDependency()
    {
        var workHandler1 = CreateTestWorkHandler();
        var workHandler2 = CreateTestWorkHandler();
        var delayedEventHandler = CreateDelayedEventHandler();
        _disruptor.HandleEventsWithWorkerPool(workHandler1, workHandler2).Then(delayedEventHandler);

        PublishEvent();
        PublishEvent();

        Assert.That(_disruptor.GetDependentSequencesFor(delayedEventHandler)?.Value, Is.EqualTo(-1L));

        workHandler2.ProcessEvent();
        workHandler1.ProcessEvent();

        delayedEventHandler.ProcessEvent();
    }

    [Test]
    public void ShouldSupportUsingWorkerPoolAsDependencyAndProcessFirstEventAsSoonAsItIsAvailable()
    {
        var workHandler1 = CreateTestWorkHandler();
        var workHandler2 = CreateTestWorkHandler();
        var delayedEventHandler = CreateDelayedEventHandler();
        _disruptor.HandleEventsWithWorkerPool(workHandler1, workHandler2).Then(delayedEventHandler);

        PublishEvent();
        PublishEvent();

        workHandler1.ProcessEvent();
        delayedEventHandler.ProcessEvent();

        workHandler2.ProcessEvent();
        delayedEventHandler.ProcessEvent();
    }

    [Test]
    public void ShouldSupportUsingWorkerPoolWithADependency()
    {
        var workHandler1 = CreateTestWorkHandler();
        var workHandler2 = CreateTestWorkHandler();
        var delayedEventHandler = CreateDelayedEventHandler();
        _disruptor.HandleEventsWith(delayedEventHandler).ThenHandleEventsWithWorkerPool(workHandler1, workHandler2);

        PublishEvent();
        PublishEvent();

        delayedEventHandler.ProcessEvent();
        delayedEventHandler.ProcessEvent();

        workHandler1.ProcessEvent();
        workHandler2.ProcessEvent();
    }

    [Test]
    public void ShouldSupportCombiningWorkerPoolWithEventHandlerAsDependencyWhenNotPreviouslyRegistered()

    {
        var workHandler1 = CreateTestWorkHandler();
        var delayedEventHandler1 = CreateDelayedEventHandler();
        var delayedEventHandler2 = CreateDelayedEventHandler();
        _disruptor.HandleEventsWith(delayedEventHandler1).And(_disruptor.HandleEventsWithWorkerPool(workHandler1)).Then(
            delayedEventHandler2);

        PublishEvent();
        PublishEvent();

        delayedEventHandler1.ProcessEvent();
        delayedEventHandler1.ProcessEvent();

        workHandler1.ProcessEvent();
        delayedEventHandler2.ProcessEvent();

        workHandler1.ProcessEvent();
        delayedEventHandler2.ProcessEvent();
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
        var eventHandler = new TestEventHandler<TestEvent>(e => remainingCapacity[0] = _disruptor.RingBuffer.GetRemainingCapacity());

        _disruptor.HandleEventsWith(eventHandler);

        //When
        PublishEvent();

        //Then
        while (remainingCapacity[0] == -1)
        {
            Thread.Sleep(100);
        }
        Assert.That(remainingCapacity[0], Is.EqualTo(_disruptor.RingBuffer.BufferSize - 1L));
        Assert.That(_disruptor.RingBuffer.GetRemainingCapacity(), Is.EqualTo(_disruptor.RingBuffer.BufferSize - 0L));
    }

    [Test]
    public void ShouldAllowEventHandlerWithSuperType()
    {
        var latch = new CountdownEvent(2);
        var objectHandler = new CountDownEventHandler<object>(latch);

        _disruptor.HandleEventsWith(objectHandler);

        EnsureTwoEventsProcessedAccordingToDependencies(latch);
    }

    [Test]
    public void ShouldAllowChainingEventHandlersWithSuperType()
    {
        var latch = new CountdownEvent(2);
        var delayedEventHandler = CreateDelayedEventHandler();
        var objectHandler = new CountDownEventHandler<object>(latch);

        _disruptor.HandleEventsWith(delayedEventHandler).Then(objectHandler);

        EnsureTwoEventsProcessedAccordingToDependencies(latch, delayedEventHandler);
    }

    [Test]
    public void ShouldMakeEntriesAvailableToFirstCustomProcessorsImmediately()
    {
        var countDownLatch = new CountdownEvent(2);
        var eventHandler = new CountDownEventHandler<TestEvent>(countDownLatch);

        _disruptor.HandleEventsWith((rb, sequenceBarrier) =>
        {
            Assert.That(sequenceBarrier.DependentSequences.DependsOnCursor, "Should depend on cursor");
            return EventProcessorFactory.Create(_disruptor.RingBuffer, sequenceBarrier, eventHandler);
        });

        EnsureTwoEventsProcessedAccordingToDependencies(countDownLatch);
    }

    [Test]
    public void ShouldHonorDependenciesForCustomProcessors()
    {
        var countDownLatch = new CountdownEvent(2);
        var eventHandler = new CountDownEventHandler<TestEvent>(countDownLatch);
        var delayedEventHandler = CreateDelayedEventHandler();

        _disruptor.HandleEventsWith(delayedEventHandler).Then((ringBuffer, sequenceBarrier) =>
        {
            Assert.That(!sequenceBarrier.DependentSequences.DependsOnCursor, "Should not depend on cursor");
            Assert.That(sequenceBarrier.DependentSequences.DependentSequenceCount, Is.EqualTo(1), "Should have had a barrier sequence");
            return EventProcessorFactory.Create(ringBuffer, sequenceBarrier, eventHandler);
        });

        EnsureTwoEventsProcessedAccordingToDependencies(countDownLatch, delayedEventHandler);
    }

    [Test]
    public void ShouldCreateDedicatedSequenceBarriersForCustomEventProcessors()
    {
        TestEventProcessor? s1 = null;
        TestEventProcessor? s2 = null;

        _disruptor.HandleEventsWith(
            (_, sb) => s1 = new TestEventProcessor(sb),
            (_, sb) => s2 = new TestEventProcessor(sb)
        );

        Assert.That(s1, Is.Not.Null);
        Assert.That(s2, Is.Not.Null);
        Assert.That(s1!.SequenceBarrier, Is.Not.SameAs(s2!.SequenceBarrier));
    }

    [Test]
    public void ShouldWaitForHandlerStartBeforeCompletingStartTask()
    {
        var startSignal1 = new ManualResetEventSlim();
        var startSignal2 = new ManualResetEventSlim();
        var handler1 = new TestEventHandler<TestEvent> { OnStartAction = () => startSignal1.Wait() };
        var handler2 = new TestEventHandler<TestEvent> { OnStartAction = () => startSignal2.Wait() };
        _disruptor.HandleEventsWith(handler1, handler2);

        var startTask = _disruptor.Start();
        Assert.That(startTask.Wait(50), Is.False);

        startSignal1.Set();
        Assert.That(startTask.Wait(50), Is.False);

        startSignal2.Set();
        Assert.That(startTask.Wait(500), Is.True);
    }

    [Test]
    public void ShouldWaitForHandlerShutdownBeforeCompletingHaltTask()
    {
        var shutdownSignal1 = new ManualResetEventSlim();
        var shutdownSignal2 = new ManualResetEventSlim();
        var handler1 = new TestEventHandler<TestEvent> { OnShutdownAction = () => shutdownSignal1.Wait() };
        var handler2 = new TestEventHandler<TestEvent> { OnShutdownAction = () => shutdownSignal2.Wait() };
        _disruptor.HandleEventsWith(handler1, handler2);

        _disruptor.Start().Wait();

        var haltTask = _disruptor.Halt();
        Assert.That(haltTask.Wait(50), Is.False);

        shutdownSignal1.Set();
        Assert.That(haltTask.Wait(50), Is.False);

        shutdownSignal2.Set();
        Assert.That(haltTask.Wait(500), Is.True);
    }

    [Test]
    public void ShouldHaltOnDispose()
    {
        _disruptor.HandleEventsWith(new TestEventHandler<TestEvent>());
        _disruptor.Start();

        Assert.That(_disruptor.IsRunning);

        _disruptor.Dispose();

        Assert.That(!_disruptor.IsRunning);
        Assert.That(_taskScheduler.JoinAllThreads(500));
    }

    private TestWorkHandler CreateTestWorkHandler()
    {
        var testWorkHandler = new TestWorkHandler();
        _testWorkHandlers.Add(testWorkHandler);
        return testWorkHandler;
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

    private void PublishEvent()
    {
        if (!_disruptor.HasStarted)
        {
            _disruptor.Start();

            foreach (var eventHandler in _delayedEventHandlers)
            {
                eventHandler.AwaitStart();
            }
        }

        _disruptor.PublishEvent().Dispose();
    }

    private static Exception WaitFor(AtomicReference<Exception> reference)
    {
        while (true)
        {
            if (reference.Read() is { } exception)
                return exception;

            Thread.Yield();
        }
    }

    private DelayedEventHandler CreateDelayedEventHandler(int? maxBatchSize = null)
    {
        var delayedEventHandler = new DelayedEventHandler { MaxBatchSize = maxBatchSize };
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
        Assert.That(released, "Batch handler did not receive entries: " + countDownLatch.CurrentCount);
    }
}
