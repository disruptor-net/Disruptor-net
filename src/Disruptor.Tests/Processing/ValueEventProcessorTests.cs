using System;
using System.Collections.Generic;
using System.Threading;
using Disruptor.Processing;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests.Processing;

[TestFixture]
public class ValueEventProcessorTests
{
    private readonly ValueRingBuffer<StubValueEvent> _ringBuffer;
    private readonly SequenceBarrier _sequenceBarrier;

    public ValueEventProcessorTests()
    {
        _ringBuffer = new ValueRingBuffer<StubValueEvent>(() => new StubValueEvent(-1), 16);
        _sequenceBarrier = _ringBuffer.NewBarrier();
    }

    private static IValueEventProcessor<T> CreateEventProcessor<T>(IValueDataProvider<T> dataProvider, SequenceBarrier sequenceBarrier, IValueEventHandler<T> eventHandler)
        where T : struct
    {
        return EventProcessorFactory.Create(dataProvider, sequenceBarrier, eventHandler);
    }

    [Test]
    public void ShouldThrowExceptionOnSettingNullExceptionHandler()
    {
        var eventHandler = new TestValueEventHandler<StubValueEvent>(x => throw new NullReferenceException());
        var eventProcessor = CreateEventProcessor(_ringBuffer, _sequenceBarrier, eventHandler);

        Assert.Throws<ArgumentNullException>(() => eventProcessor.SetExceptionHandler(null!));
    }

    [Test]
    public void ShouldCallMethodsInLifecycleOrderForBatch()
    {
        var eventSignal = new CountdownEvent(3);
        var eventHandler = new TestValueEventHandler<StubValueEvent>(x => eventSignal.Signal());
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
        var ringBuffer = new ValueRingBuffer<StubValueEvent>(() => new StubValueEvent(-1), new SingleProducerSequencer(16, waitStrategy));
        var sequenceBarrier = ringBuffer.NewBarrier();

        var onTimeoutSignal = new ManualResetEvent(false);
        var eventHandler = new TestValueEventHandler<StubValueEvent> { OnTimeoutAction = () => onTimeoutSignal.Set() };
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
        var ringBuffer = new ValueRingBuffer<StubValueEvent>(() => new StubValueEvent(-1), new SingleProducerSequencer(16, waitStrategy));
        var sequenceBarrier = ringBuffer.NewBarrier();

        var exceptionSignal = new CountdownEvent(1);
        var exceptionHandler = new TestValueExceptionHandler<StubValueEvent>(x => exceptionSignal.Signal());
        var eventHandler = new TestValueEventHandler<StubValueEvent> { OnTimeoutAction = TestException.ThrowOnce() };
        var eventProcessor = CreateEventProcessor(ringBuffer, sequenceBarrier, eventHandler);
        ringBuffer.AddGatingSequences(eventProcessor.Sequence);

        eventProcessor.SetExceptionHandler(exceptionHandler);

        var task = eventProcessor.Start();

        Assert.That(exceptionSignal.Wait(TimeSpan.FromSeconds(2)));
        Assert.That(exceptionHandler.EventExceptionCount, Is.EqualTo(0));
        Assert.That(exceptionHandler.TimeoutExceptionCount, Is.EqualTo(1));

        eventProcessor.Halt();

        Assert.That(task.Wait(TimeSpan.FromSeconds(2)));
    }

    [Test]
    public void ShouldCallExceptionHandlerOnUncaughtException()
    {
        var exceptionSignal = new CountdownEvent(1);
        var exceptionHandler = new TestValueExceptionHandler<StubValueEvent>(x => exceptionSignal.Signal());
        var eventHandler = new TestValueEventHandler<StubValueEvent>(x => throw new NullReferenceException());
        var eventProcessor = CreateEventProcessor(_ringBuffer, _sequenceBarrier, eventHandler);
        _ringBuffer.AddGatingSequences(eventProcessor.Sequence);

        eventProcessor.SetExceptionHandler(exceptionHandler);

        var task = eventProcessor.Start();

        _ringBuffer.PublishStubEvent(0);

        Assert.That(exceptionSignal.Wait(TimeSpan.FromSeconds(2)));
        Assert.That(exceptionHandler.EventExceptionCount, Is.EqualTo(1));
        Assert.That(exceptionHandler.TimeoutExceptionCount, Is.EqualTo(0));

        eventProcessor.Halt();

        Assert.That(task.Wait(TimeSpan.FromSeconds(2)));
    }

    [Test]
    public void ShouldCallExceptionHandlerOnMultipleUncaughtException()
    {
        var processingSignal = new CountdownEvent(5);
        var exceptionHandler = new TestValueExceptionHandler<StubValueEvent>(x => processingSignal.Signal());
        var eventHandler = new TestValueEventHandler<StubValueEvent>(x =>
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

        eventProcessor.Halt();

        Assert.That(task.Wait(TimeSpan.FromSeconds(2)));
    }

    [Test]
    public void ReportAccurateBatchSizesAtBatchStartTime()
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

    private class LoopbackEventHandler : IValueEventHandler<StubValueEvent>
    {
        private readonly List<long> _batchSizes;
        private readonly ValueRingBuffer<StubValueEvent> _ringBuffer;
        private readonly CountdownEvent _signal;

        public LoopbackEventHandler(ValueRingBuffer<StubValueEvent> ringBuffer, List<long> batchSizes, CountdownEvent signal)
        {
            _batchSizes = batchSizes;
            _ringBuffer = ringBuffer;
            _signal = signal;
        }

        public void OnBatchStart(long batchSize) => _batchSizes.Add(batchSize);

        public void OnEvent(ref StubValueEvent data, long sequence, bool endOfBatch)
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
        var dp = new ArrayValueDataProvider<StubValueEvent>(sequencer.BufferSize);

        var h1 = new LifeCycleHandler();
        var p1 = CreateEventProcessor(dp, barrier, h1);

        p1.Halt();
        p1.Start();

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

    private class LifeCycleHandler : IValueEventHandler<StubValueEvent>
    {
        private readonly ManualResetEvent _startedSignal = new(false);
        private readonly ManualResetEvent _shutdownSignal = new(false);

        public void OnEvent(ref StubValueEvent data, long sequence, bool endOfBatch)
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
        var ringBuffer = new ValueRingBuffer<StubValueEvent>(() => new StubValueEvent(-1), new MultiProducerSequencer(16));
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

        Assert.That(eventHandler.BatchSizeToCount.Count, Is.Not.EqualTo(0));
        Assert.That(eventHandler.BatchSizeToCount.Keys, Has.No.Member(0));
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

    public class LimitedBatchSizeEventHandler : IValueEventHandler<StubValueEvent>
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

        public void OnEvent(ref StubValueEvent data, long sequence, bool endOfBatch)
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
    public class BatchAwareEventHandler : IValueEventHandler<StubValueEvent>
    {
        public Dictionary<long, int> BatchSizeToCount { get; } = new();

        public void OnEvent(ref StubValueEvent data, long sequence, bool endOfBatch)
        {
        }

        public void OnBatchStart(long batchSize)
        {
            BatchSizeToCount[batchSize] = BatchSizeToCount.TryGetValue(batchSize, out var count) ? count + 1 : 1;
        }
    }

    internal class BatchAwareEventHandlerInternal : BatchAwareEventHandler
    {
    }

    public class ExplicitBatchStartImplementationEventHandler : IValueEventHandler<StubValueEvent>
    {
        private readonly CountdownEvent _signal;

        public int BatchCount { get; private set; }

        public ExplicitBatchStartImplementationEventHandler(CountdownEvent signal)
        {
            _signal = signal;
        }

        public void OnEvent(ref StubValueEvent data, long sequence, bool endOfBatch)
        {
            _signal.Signal();
        }

        void IValueEventHandler<StubValueEvent>.OnBatchStart(long batchSize)
        {
            ++BatchCount;
        }
    }
}
