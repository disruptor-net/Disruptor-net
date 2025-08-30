using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.Tests.Dsl.Stubs;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests.Dsl;

[TestFixture]
public class IpcDisruptorTests : IAsyncDisposable
{
    private const int _timeoutInSeconds = 2;
    private readonly IpcDisruptor<TestValueEvent> _disruptor;
    private readonly StubTaskScheduler _taskScheduler = new();
    private readonly List<DelayedEventHandler> _delayedEventHandlers = new();

    public IpcDisruptorTests()
    {
        _disruptor = new IpcDisruptor<TestValueEvent>(4, new YieldingWaitStrategy(), _taskScheduler);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var delayedEventHandler in _delayedEventHandlers)
        {
            delayedEventHandler.StopWaiting();
        }

        await _disruptor.DisposeAsync();

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
    public void ShouldPublishAndHandleEvent()
    {
        var eventCounter = new CountdownEvent(2);
        var values = new List<int>();

        _disruptor.HandleEventsWith(new TestValueEventHandler<TestValueEvent>(e => values.Add(e.Value)))
                  .Then(new TestValueEventHandler<TestValueEvent>(e => eventCounter.Signal()));

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
    public void ShouldPublishAndHandleEvents()
    {
        var eventCounter = new CountdownEvent(4);
        var values = new List<int>();

        _disruptor.HandleEventsWith(new TestValueEventHandler<TestValueEvent>(e => values.Add(e.Value)))
                  .Then(new TestValueEventHandler<TestValueEvent>(e => eventCounter.Signal()));

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
        var h1 = new SleepingEventHandler();
        var h2 = new SleepingEventHandler();
        var h3 = new SleepingEventHandler();

        rb.Publish(rb.Next());
        rb.Publish(rb.Next());
        rb.Publish(rb.Next());
        rb.Publish(rb.Next());
        rb.Publish(rb.Next());
        rb.Publish(rb.Next());

        _disruptor.HandleEventsWith(h1).Then(h2).Then(h3);

        Assert.That(_disruptor.GetSequenceValueFor(h1), Is.EqualTo(5L));
        Assert.That(_disruptor.GetSequenceValueFor(h2), Is.EqualTo(5L));
        Assert.That(_disruptor.GetSequenceValueFor(h3), Is.EqualTo(5L));
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
        using var _ = _taskScheduler.SuspendExecutions();

        var eventHandler1 = new SleepingEventHandler();
        var eventHandler2 = new SleepingEventHandler();

        var eventHandlerGroup = _disruptor.HandleEventsWith(eventHandler1, eventHandler2);
        Assert.That(eventHandlerGroup, Is.Not.Null);

        _disruptor.Start();
        Assert.That(_taskScheduler.TaskCount, Is.EqualTo(2));
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

        Assert.That(_taskScheduler.TaskCount, Is.EqualTo(3));
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
        Assert.That(actualException, Is.SameAs(testException));
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
        try
        {
            stubPublisher.Start();

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
        var eventHandler = new TestValueEventHandler<TestValueEvent>(e => remainingCapacity[0] = _disruptor.RingBuffer.GetRemainingCapacity());

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
    public void ShouldWaitForHandlerStartBeforeCompletingStartTask()
    {
        var startSignal1 = new ManualResetEventSlim();
        var startSignal2 = new ManualResetEventSlim();
        var handler1 = new TestValueEventHandler<TestValueEvent> { OnStartAction = () => startSignal1.Wait() };
        var handler2 = new TestValueEventHandler<TestValueEvent> { OnStartAction = () => startSignal2.Wait() };
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
        var handler1 = new TestValueEventHandler<TestValueEvent> { OnShutdownAction = () => shutdownSignal1.Wait() };
        var handler2 = new TestValueEventHandler<TestValueEvent> { OnShutdownAction = () => shutdownSignal2.Wait() };
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
    public async Task ShouldHaltAndDisposeMemoryOnDispose()
    {
        _disruptor.HandleEventsWith(new TestValueEventHandler<TestValueEvent>());
        await _disruptor.Start();

        Assert.That(_disruptor.IsRunning);

        // ReSharper disable once MethodHasAsyncOverload
        _disruptor.Dispose();

        Assert.That(!_disruptor.IsRunning);
        Assert.That(_taskScheduler.JoinAllThreads(500));
        Assert.That(_disruptor.IpcDirectoryPath, Does.Not.Exist);
    }

    [Test]
    public async Task ShouldHaltAndDisposeMemoryOnDisposeAsync()
    {
        _disruptor.HandleEventsWith(new TestValueEventHandler<TestValueEvent>());
        await _disruptor.Start();

        Assert.That(_disruptor.IsRunning);

        await _disruptor.DisposeAsync();

        Assert.That(!_disruptor.IsRunning);
        Assert.That(_taskScheduler.JoinAllThreads(500));
        Assert.That(_disruptor.IpcDirectoryPath, Does.Not.Exist);
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
        Assert.That(released, "Batch handler did not receive entries: " + countDownLatch.CurrentCount);
    }
}
