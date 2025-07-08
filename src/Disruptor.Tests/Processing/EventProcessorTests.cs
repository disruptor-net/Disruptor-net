using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests.Processing;

[TestFixture]
public class EventProcessorTests
{
    private readonly RingBuffer<StubEvent> _ringBuffer;
    private readonly SequenceBarrier _sequenceBarrier;

    public EventProcessorTests()
    {
        _ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(-1), 16);
        _sequenceBarrier = _ringBuffer.NewBarrier();
    }

    private static IEventProcessor<T> CreateEventProcessor<T>(IDataProvider<T> dataProvider, SequenceBarrier sequenceBarrier, IEventHandler<T> eventHandler)
        where T : class
    {
        return EventProcessorFactory.Create(dataProvider, sequenceBarrier, eventHandler);
    }

    [Test]
    public void ShouldThrowExceptionOnSettingNullExceptionHandler()
    {
        var eventHandler = new TestEventHandler<StubEvent>(x => throw new NullReferenceException());
        var eventProcessor = CreateEventProcessor(_ringBuffer, _sequenceBarrier, eventHandler);

        Assert.Throws<ArgumentNullException>(() => eventProcessor.SetExceptionHandler(null!));
    }

    [Test]
    public void ShouldCallMethodsInLifecycleOrderForBatch()
    {
        var eventSignal = new CountdownEvent(3);
        var eventHandler = new TestEventHandler<StubEvent>(x => eventSignal.Signal());
        var eventProcessor = CreateEventProcessor(_ringBuffer, _sequenceBarrier, eventHandler);

        _ringBuffer.AddGatingSequences(eventProcessor.Sequence);

        _ringBuffer.PublishStubEvent(0);
        _ringBuffer.PublishStubEvent(0);
        _ringBuffer.PublishStubEvent(0);

        var task = eventProcessor.Start();

        Assert.That(eventSignal.Wait(TimeSpan.FromSeconds(2)));

        eventProcessor.Halt();

        Assert.That(task.Wait(TimeSpan.FromSeconds(2)));
    }

    [Test]
    public void ShouldCallOnTimeout()
    {
        var waitStrategy = new TimeoutBlockingWaitStrategy(TimeSpan.FromMilliseconds(1));
        var ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(-1), new SingleProducerSequencer(16, waitStrategy));
        var sequenceBarrier = ringBuffer.NewBarrier();

        var onTimeoutSignal = new ManualResetEvent(false);
        var eventHandler = new TestEventHandler<StubEvent> { OnTimeoutAction = () => onTimeoutSignal.Set() };
        var eventProcessor = CreateEventProcessor(ringBuffer, sequenceBarrier, eventHandler);
        ringBuffer.AddGatingSequences(eventProcessor.Sequence);

        var task = eventProcessor.Start();

        Assert.That(onTimeoutSignal.WaitOne(TimeSpan.FromSeconds(2)));

        eventProcessor.Halt();

        Assert.That(task.Wait(TimeSpan.FromSeconds(2)));
    }

    [Test]
    public void ShouldCallExceptionHandlerOnTimeoutException()
    {
        var waitStrategy = new TimeoutBlockingWaitStrategy(TimeSpan.FromMilliseconds(1));
        var ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(-1), new SingleProducerSequencer(16, waitStrategy));
        var sequenceBarrier = ringBuffer.NewBarrier();

        var exception = new TaskCompletionSource<Exception>();
        var exceptionHandler = new TestExceptionHandler<StubEvent>(x => exception.TrySetResult(x.ex));
        var eventHandler = new TestEventHandler<StubEvent> { OnTimeoutAction = TestException.ThrowOnce() };
        var eventProcessor = CreateEventProcessor(ringBuffer, sequenceBarrier, eventHandler);
        ringBuffer.AddGatingSequences(eventProcessor.Sequence);

        eventProcessor.SetExceptionHandler(exceptionHandler);

        var task = eventProcessor.Start();

        Assert.That(exception.Task.Wait(TimeSpan.FromSeconds(2)));
        Assert.That(exceptionHandler.EventExceptionCount, Is.EqualTo(0));
        Assert.That(exceptionHandler.TimeoutExceptionCount, Is.EqualTo(1));
        Assert.That(exceptionHandler.BatchExceptionCount, Is.EqualTo(0));

        eventProcessor.Halt();

        Assert.That(task.Wait(TimeSpan.FromSeconds(2)));
    }

    [Test]
    public void ShouldCallExceptionHandlerOnUncaughtException()
    {
        var exceptionSignal = new CountdownEvent(1);
        var exceptionHandler = new TestExceptionHandler<StubEvent>(x => exceptionSignal.Signal());
        var eventHandler = new TestEventHandler<StubEvent>(x => throw new NullReferenceException());
        var eventProcessor = CreateEventProcessor(_ringBuffer, _sequenceBarrier, eventHandler);
        _ringBuffer.AddGatingSequences(eventProcessor.Sequence);

        eventProcessor.SetExceptionHandler(exceptionHandler);

        var task = eventProcessor.Start();

        _ringBuffer.PublishStubEvent(0);

        Assert.That(exceptionSignal.Wait(TimeSpan.FromSeconds(2)));
        Assert.That(exceptionHandler.EventExceptionCount, Is.EqualTo(1));
        Assert.That(exceptionHandler.TimeoutExceptionCount, Is.EqualTo(0));
        Assert.That(exceptionHandler.BatchExceptionCount, Is.EqualTo(0));

        eventProcessor.Halt();

        Assert.That(task.Wait(TimeSpan.FromSeconds(2)));
    }

    [Test]
    public void ShouldCallExceptionHandlerOnMultipleUncaughtException()
    {
        var processingSignal = new CountdownEvent(5);
        var exceptionHandler = new TestExceptionHandler<StubEvent>(x => processingSignal.Signal());
        var eventHandler = new TestEventHandler<StubEvent>(x =>
        {
            if (x.Value == 1)
                throw new Exception();

            processingSignal.Signal();
        });
        var eventProcessor = CreateEventProcessor(_ringBuffer, _sequenceBarrier, eventHandler);
        _ringBuffer.AddGatingSequences(eventProcessor.Sequence);

        eventProcessor.SetExceptionHandler(exceptionHandler);

        var task = eventProcessor.Start();

        _ringBuffer.PublishStubEvent(0);
        _ringBuffer.PublishStubEvent(1);
        _ringBuffer.PublishStubEvent(0);
        _ringBuffer.PublishStubEvent(1);
        _ringBuffer.PublishStubEvent(0);

        Assert.That(processingSignal.Wait(TimeSpan.FromSeconds(2)));
        Assert.That(exceptionHandler.EventExceptionCount, Is.EqualTo(2));
        Assert.That(exceptionHandler.TimeoutExceptionCount, Is.EqualTo(0));
        Assert.That(exceptionHandler.BatchExceptionCount, Is.EqualTo(0));

        eventProcessor.Halt();

        Assert.That(task.Wait(TimeSpan.FromSeconds(2)));
    }

    [Test]
    public void ShouldReportAccurateBatchSizesAtBatchStartTime()
    {
        var batchSizes = new List<long>();
        var signal = new CountdownEvent(6);

        var eventProcessor = CreateEventProcessor(_ringBuffer, _sequenceBarrier, new LoopbackEventHandler(_ringBuffer, batchSizes, signal));

        _ringBuffer.PublishStubEvent(0);
        _ringBuffer.PublishStubEvent(0);
        _ringBuffer.PublishStubEvent(0);

        var task = eventProcessor.Start();
        Assert.That(signal.Wait(TimeSpan.FromSeconds(2)));

        eventProcessor.Halt();

        Assert.That(task.Wait(TimeSpan.FromSeconds(2)));
        Assert.That(batchSizes, Is.EqualTo(new List<long> { 3, 2, 1 }));
    }

    [Test]
    public void ShouldIgnorePreviouslyPublishedEvents()
    {
        _ringBuffer.PublishStubEvent(0);
        _ringBuffer.PublishStubEvent(1);
        _ringBuffer.PublishStubEvent(2);
        _ringBuffer.PublishStubEvent(3);

        var capturedValues = new List<int>();
        var completed = new ManualResetEventSlim();
        var eventHandler = new TestEventHandler<StubEvent>(x =>
        {
            capturedValues.Add(x.Value);

            if (x.Value == 5)
                completed.Set();
        });

        var eventProcessor = CreateEventProcessor(_ringBuffer, _sequenceBarrier, eventHandler);
        _ringBuffer.AddGatingSequences(eventProcessor.Sequence);

        var task = eventProcessor.Start();

        _ringBuffer.PublishStubEvent(4);
        _ringBuffer.PublishStubEvent(5);

        Assert.That(completed.Wait(TimeSpan.FromSeconds(2)));
        Assert.That(capturedValues, Is.EquivalentTo(new[] { 4, 5 }));

        eventProcessor.Halt();

        Assert.That(task.Wait(TimeSpan.FromSeconds(2)));
    }

    private class LoopbackEventHandler : IEventHandler<StubEvent>
    {
        private readonly List<long> _batchSizes;
        private readonly RingBuffer<StubEvent> _ringBuffer;
        private readonly CountdownEvent _signal;

        public LoopbackEventHandler(RingBuffer<StubEvent> ringBuffer, List<long> batchSizes, CountdownEvent signal)
        {
            _batchSizes = batchSizes;
            _ringBuffer = ringBuffer;
            _signal = signal;
        }

        public void OnBatchStart(long batchSize) => _batchSizes.Add(batchSize);

        public void OnEvent(StubEvent data, long sequence, bool endOfBatch)
        {
            if (!endOfBatch)
            {
                _ringBuffer.PublishStubEvent(0);
            }

            _signal.Signal();
        }
    }

    [Test]
    public void ShouldAlwaysHalt()
    {
        var waitStrategy = new BusySpinWaitStrategy();
        var sequencer = new SingleProducerSequencer(8, waitStrategy);
        var sequenceWaiter = waitStrategy.NewSequenceWaiter(SequenceWaiterOwner.Unknown, new DependentSequenceGroup(new Sequence()));
        var barrier = new SequenceBarrier(sequencer, sequenceWaiter);
        var dp = new ArrayDataProvider<StubEvent>(sequencer.BufferSize);
        var delayedTaskScheduler = new DelayedTaskScheduler();

        var h1 = new LifeCycleHandler();
        var p1 = CreateEventProcessor(dp, barrier, h1);

        p1.Start(delayedTaskScheduler, TaskCreationOptions.None);
        p1.Halt();
        delayedTaskScheduler.StartPendingTasks();

        Assert.That(h1.WaitStart(TimeSpan.FromSeconds(2)));
        Assert.That(h1.WaitShutdown(TimeSpan.FromSeconds(2)));

        for (int i = 0; i < 1000; i++)
        {
            var h2 = new LifeCycleHandler();
            var p2 = CreateEventProcessor(dp, barrier, h2);
            p2.Start();

            p2.Halt();

            Assert.That(h2.WaitStart(TimeSpan.FromSeconds(2)));
            Assert.That(h2.WaitShutdown(TimeSpan.FromSeconds(2)));
        }

        for (int i = 0; i < 1000; i++)
        {
            var h2 = new LifeCycleHandler();
            var p2 = CreateEventProcessor(dp, barrier, h2);

            p2.Start();
            Thread.Yield();
            p2.Halt();

            Assert.That(h2.WaitStart(TimeSpan.FromSeconds(2)));
            Assert.That(h2.WaitShutdown(TimeSpan.FromSeconds(2)));
        }
    }

    [Test]
    public void ShouldInvokeOnStartAndOnShutdown()
    {
        var handler = new LifeCycleHandler();
        var processor = CreateEventProcessor(_ringBuffer, _sequenceBarrier, handler);

        var task = processor.Start();

        var wasStarted = handler.WaitStart(TimeSpan.FromMilliseconds(500));
        Assert.That(wasStarted);

        var wasShutdownAfterStart = handler.WaitShutdown(TimeSpan.FromMilliseconds(10));
        Assert.That(!wasShutdownAfterStart);

        processor.Halt();

        var stopped = task.Wait(TimeSpan.FromMilliseconds(500));
        Assert.That(stopped);

        var wasShutdownAfterStop = handler.WaitShutdown(TimeSpan.FromMilliseconds(10));
        Assert.That(wasShutdownAfterStop);
    }

    private class LifeCycleHandler : IEventHandler<StubEvent>
    {
        private readonly ManualResetEvent _startedSignal = new(false);
        private readonly ManualResetEvent _shutdownSignal = new(false);

        public void OnEvent(StubEvent data, long sequence, bool endOfBatch)
        {
        }

        public void OnStart()
        {
            _startedSignal.Set();
        }

        public void OnShutdown()
        {
            _shutdownSignal.Set();
        }

        public bool WaitStart(TimeSpan timeSpan)
        {
            return _startedSignal.WaitOne(timeSpan);
        }

        public bool WaitShutdown(TimeSpan timeSpan)
        {
            return _shutdownSignal.WaitOne(timeSpan);
        }
    }

    [TestCase(typeof(BatchAwareEventHandler))]
    [TestCase(typeof(BatchAwareEventHandlerInternal))]
    public void ShouldNotPassZeroSizeToBatchStartAware(Type eventHandlerType)
    {
        var ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(-1), new MultiProducerSequencer(16));
        var sequenceBarrier = ringBuffer.NewBarrier();

        var eventHandler = (BatchAwareEventHandler)Activator.CreateInstance(eventHandlerType)!;

        var eventProcessor = CreateEventProcessor(ringBuffer, sequenceBarrier, eventHandler);

        ringBuffer.AddGatingSequences(eventProcessor.Sequence);

        var task = eventProcessor.Start();

        for (var i = 0; i < 3; i++)
        {
            var sequence = ringBuffer.Next();
            Thread.Sleep(100);

            ringBuffer.Publish(sequence);
        }

        eventProcessor.Halt();
        task.Wait();

        Assert.That(eventHandler.BatchSizes.Count, Is.Not.EqualTo(0));
        Assert.That(eventHandler.BatchSizes, Has.None.EqualTo(0));
    }

    [Test]
    public void ShouldCallBatchStartWithExplicitImplementation()
    {
        var signal = new CountdownEvent(1);

        var eventHandler = new ExplicitBatchStartImplementationEventHandler(signal);
        var eventProcessor = CreateEventProcessor(_ringBuffer, _sequenceBarrier, eventHandler);

        _ringBuffer.PublishStubEvent(0);

        var task = eventProcessor.Start();
        Assert.That(signal.Wait(TimeSpan.FromSeconds(2)));

        eventProcessor.Halt();

        Assert.That(task.Wait(TimeSpan.FromSeconds(2)));
        Assert.That(eventHandler.BatchCount, Is.EqualTo(1));
    }

    [Test]
    public void ShouldLimitMaxBatchSize()
    {
        const int eventCountCount = 15;

        var eventSignal = new CountdownEvent(eventCountCount);
        var eventHandler = new LimitedBatchSizeEventHandler(6, eventSignal);

        var eventProcessor = CreateEventProcessor(_ringBuffer, _sequenceBarrier, eventHandler);
        _ringBuffer.AddGatingSequences(eventProcessor.Sequence);

        for (var i = 0; i < eventCountCount; i++)
        {
            _ringBuffer.PublishStubEvent(i);
        }

        eventProcessor.Start();

        Assert.That(eventSignal.Wait(TimeSpan.FromSeconds(2)));
        Assert.That(eventHandler.BatchSizes, Is.EqualTo(new List<long> { 6, 6, 3 }));

        eventProcessor.Halt();
    }

    public class LimitedBatchSizeEventHandler : IEventHandler<StubEvent>
    {
        private readonly CountdownEvent _countdownEvent;
        private int _currentBatchSize;

        public LimitedBatchSizeEventHandler(int maxBatchSize, CountdownEvent countdownEvent)
        {
            MaxBatchSize = maxBatchSize;
            _countdownEvent = countdownEvent;
        }

        public int? MaxBatchSize { get; }
        public List<long> BatchSizes { get; } = new();

        public void OnEvent(StubEvent data, long sequence, bool endOfBatch)
        {
            _currentBatchSize++;

            if (endOfBatch)
            {
                BatchSizes.Add(_currentBatchSize);
                _currentBatchSize = 0;
            }

            _countdownEvent.Signal();
        }
    }

    // ReSharper disable once MemberCanBePrivate.Global
    // Public to enable dynamic code generation
    public class BatchAwareEventHandler : IEventHandler<StubEvent>
    {
        public List<long> BatchSizes { get; } = new();

        public void OnEvent(StubEvent data, long sequence, bool endOfBatch)
        {
        }

        public void OnBatchStart(long batchSize)
        {
            BatchSizes.Add(batchSize);
        }
    }

    internal class BatchAwareEventHandlerInternal : BatchAwareEventHandler
    {
    }

    public class ExplicitBatchStartImplementationEventHandler : IEventHandler<StubEvent>
    {
        private readonly CountdownEvent _signal;

        public int BatchCount { get; private set; }

        public ExplicitBatchStartImplementationEventHandler(CountdownEvent signal)
        {
            _signal = signal;
        }


        public void OnEvent(StubEvent data, long sequence, bool endOfBatch)
        {
            _signal.Signal();
        }

        void IEventHandler<StubEvent>.OnBatchStart(long batchSize)
        {
            ++BatchCount;
        }
    }
}
