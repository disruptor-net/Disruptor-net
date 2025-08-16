using System;
using System.Collections.Generic;
using System.Threading;
using Disruptor.Processing;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests.Processing;

[TestFixture]
public class IpcEventProcessorTests : IDisposable
{
    private readonly IpcRingBuffer<StubUnmanagedEvent> _ringBuffer;

    public IpcEventProcessorTests()
    {
        var memory = IpcRingBufferMemory.CreateTemporary(16, sequencerCapacity: 2048, initializer: _ => new StubUnmanagedEvent(-1));
        _ringBuffer = new IpcRingBuffer<StubUnmanagedEvent>(memory, new YieldingWaitStrategy(), true);
    }

    public void Dispose()
    {
        _ringBuffer.Dispose();
    }

    private static IIpcEventProcessor<T> CreateEventProcessor<T>(IpcRingBuffer<T> dataProvider, SequencePointer sequencePointer, IpcSequenceBarrier sequenceBarrier, IValueEventHandler<T> eventHandler)
        where T : unmanaged
    {
        return EventProcessorFactory.Create(dataProvider, sequencePointer, sequenceBarrier, eventHandler);
    }

    [Test]
    public void ShouldThrowExceptionOnSettingNullExceptionHandler()
    {
        var eventHandler = new TestValueEventHandler<StubUnmanagedEvent>(x => throw new NullReferenceException());
        var sequence = _ringBuffer.NewSequence();
        var sequenceBarrier = _ringBuffer.NewBarrier();
        var eventProcessor = CreateEventProcessor(_ringBuffer, sequence, sequenceBarrier, eventHandler);

        Assert.Throws<ArgumentNullException>(() => eventProcessor.SetExceptionHandler(null!));
    }

    [Test]
    public void ShouldCallMethodsInLifecycleOrderForBatch()
    {
        var eventSignal = new CountdownEvent(3);
        var eventHandler = new TestValueEventHandler<StubUnmanagedEvent>(x => eventSignal.Signal());
        var sequence = _ringBuffer.NewSequence();
        var sequenceBarrier = _ringBuffer.NewBarrier();
        var eventProcessor = CreateEventProcessor(_ringBuffer, sequence, sequenceBarrier, eventHandler);

        _ringBuffer.SetGatingSequences(eventProcessor.SequencePointer);

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
        var memory = IpcRingBufferMemory.CreateTemporary(16, initializer: _ => new StubUnmanagedEvent(-1));
        using var ringBuffer = new IpcRingBuffer<StubUnmanagedEvent>(memory, new TimeoutYieldingWaitStrategy(TimeSpan.FromMilliseconds(1)), true);
        var sequence = ringBuffer.NewSequence();
        var sequenceBarrier = ringBuffer.NewBarrier();

        var onTimeoutSignal = new ManualResetEvent(false);
        var eventHandler = new TestValueEventHandler<StubUnmanagedEvent> { OnTimeoutAction = () => onTimeoutSignal.Set() };
        var eventProcessor = CreateEventProcessor(ringBuffer, sequence, sequenceBarrier, eventHandler);
        ringBuffer.SetGatingSequences(eventProcessor.SequencePointer);

        var task = eventProcessor.Start();

        Assert.That(onTimeoutSignal.WaitOne(TimeSpan.FromSeconds(2)));

        eventProcessor.Halt();

        Assert.That(task.Wait(TimeSpan.FromSeconds(2)));
    }

    [Test]
    public void ShouldCallExceptionHandlerOnTimeoutException()
    {
        var memory = IpcRingBufferMemory.CreateTemporary(16, initializer: _ => new StubUnmanagedEvent(-1));
        using var ringBuffer = new IpcRingBuffer<StubUnmanagedEvent>(memory, new TimeoutYieldingWaitStrategy(TimeSpan.FromMilliseconds(1)), true);
        var sequence = ringBuffer.NewSequence();
        var sequenceBarrier = ringBuffer.NewBarrier();

        var exceptionSignal = new CountdownEvent(1);
        var exceptionHandler = new TestValueExceptionHandler<StubUnmanagedEvent>(x => exceptionSignal.Signal());
        var eventHandler = new TestValueEventHandler<StubUnmanagedEvent> { OnTimeoutAction = TestException.ThrowOnce() };
        var eventProcessor = CreateEventProcessor(ringBuffer, sequence, sequenceBarrier, eventHandler);
        ringBuffer.SetGatingSequences(eventProcessor.SequencePointer);

        eventProcessor.SetExceptionHandler(exceptionHandler);

        var task = eventProcessor.Start();

        Assert.That(exceptionSignal.Wait(TimeSpan.FromSeconds(20)));
        Assert.That(exceptionHandler.EventExceptionCount, Is.EqualTo(0));
        Assert.That(exceptionHandler.TimeoutExceptionCount, Is.EqualTo(1));

        eventProcessor.Halt();

        Assert.That(task.Wait(TimeSpan.FromSeconds(20)));

        Console.WriteLine(1);
    }

    [Test]
    public void ShouldCallExceptionHandlerOnUncaughtException()
    {
        var exceptionSignal = new CountdownEvent(1);
        var exceptionHandler = new TestValueExceptionHandler<StubUnmanagedEvent>(x => exceptionSignal.Signal());
        var eventHandler = new TestValueEventHandler<StubUnmanagedEvent>(x => throw new NullReferenceException());
        var sequence = _ringBuffer.NewSequence();
        var sequenceBarrier = _ringBuffer.NewBarrier();
        var eventProcessor = CreateEventProcessor(_ringBuffer, sequence, sequenceBarrier, eventHandler);
        _ringBuffer.SetGatingSequences(eventProcessor.SequencePointer);

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
        var exceptionHandler = new TestValueExceptionHandler<StubUnmanagedEvent>(x => processingSignal.Signal());
        var eventHandler = new TestValueEventHandler<StubUnmanagedEvent>(x =>
        {
            if (x.Value == 1)
                throw new Exception();

            processingSignal.Signal();
        });
        var sequence = _ringBuffer.NewSequence();
        var sequenceBarrier = _ringBuffer.NewBarrier();
        var eventProcessor = CreateEventProcessor(_ringBuffer, sequence, sequenceBarrier, eventHandler);
        _ringBuffer.SetGatingSequences(eventProcessor.SequencePointer);

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
        var sequence = _ringBuffer.NewSequence();
        var sequenceBarrier = _ringBuffer.NewBarrier();
        var eventProcessor = CreateEventProcessor(_ringBuffer, sequence, sequenceBarrier, new LoopbackEventHandler(_ringBuffer, batchSizes, signal));

        _ringBuffer.PublishStubEvent(0);
        _ringBuffer.PublishStubEvent(0);
        _ringBuffer.PublishStubEvent(0);

        var task = eventProcessor.Start();
        Assert.That(signal.Wait(TimeSpan.FromSeconds(2)));

        eventProcessor.Halt();

        Assert.That(task.Wait(TimeSpan.FromSeconds(2)));
        Assert.That(batchSizes, Is.EqualTo(new List<long> { 3, 2, 1 }));
    }

    private class LoopbackEventHandler : IValueEventHandler<StubUnmanagedEvent>
    {
        private readonly List<long> _batchSizes;
        private readonly IpcRingBuffer<StubUnmanagedEvent> _ringBuffer;
        private readonly CountdownEvent _signal;

        public LoopbackEventHandler(IpcRingBuffer<StubUnmanagedEvent> ringBuffer, List<long> batchSizes, CountdownEvent signal)
        {
            _batchSizes = batchSizes;
            _ringBuffer = ringBuffer;
            _signal = signal;
        }

        public void OnBatchStart(long batchSize) => _batchSizes.Add(batchSize);

        public void OnEvent(ref StubUnmanagedEvent data, long sequence, bool endOfBatch)
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
        var barrierSequence = _ringBuffer.NewSequence();
        var barrier = _ringBuffer.NewBarrier(SequenceWaiterOwner.Unknown, [barrierSequence]);
        var delayedTaskScheduler = new DelayedTaskScheduler();

        var h1 = new LifeCycleHandler();
        var s1 = _ringBuffer.NewSequence();
        var p1 = CreateEventProcessor(_ringBuffer, s1, barrier, h1);

        p1.Start(delayedTaskScheduler);
        p1.Halt();
        delayedTaskScheduler.StartPendingTasks();

        Assert.That(h1.WaitStart(TimeSpan.FromSeconds(2)));
        Assert.That(h1.WaitShutdown(TimeSpan.FromSeconds(2)));

        for (int i = 0; i < 1000; i++)
        {
            var h2 = new LifeCycleHandler();
            var s2 = _ringBuffer.NewSequence();
            var p2 = CreateEventProcessor(_ringBuffer, s2, barrier, h2);
            p2.Start();

            p2.Halt();

            Assert.That(h2.WaitStart(TimeSpan.FromSeconds(2)));
            Assert.That(h2.WaitShutdown(TimeSpan.FromSeconds(2)));
        }

        for (int i = 0; i < 1000; i++)
        {
            var h2 = new LifeCycleHandler();
            var s2 = _ringBuffer.NewSequence();
            var p2 = CreateEventProcessor(_ringBuffer, s2, barrier, h2);

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
        var sequence = _ringBuffer.NewSequence();
        var sequenceBarrier = _ringBuffer.NewBarrier();
        var processor = CreateEventProcessor(_ringBuffer, sequence, sequenceBarrier, handler);

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

    private class LifeCycleHandler : IValueEventHandler<StubUnmanagedEvent>
    {
        private readonly ManualResetEvent _startedSignal = new(false);
        private readonly ManualResetEvent _shutdownSignal = new(false);

        public void OnEvent(ref StubUnmanagedEvent data, long sequence, bool endOfBatch)
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
        var eventHandler = (BatchAwareEventHandler)Activator.CreateInstance(eventHandlerType)!;

        var sequence = _ringBuffer.NewSequence();
        var sequenceBarrier = _ringBuffer.NewBarrier();
        var eventProcessor = CreateEventProcessor(_ringBuffer, sequence, sequenceBarrier, eventHandler);

        _ringBuffer.SetGatingSequences(eventProcessor.SequencePointer);

        var task = eventProcessor.Start();

        for (var i = 0; i < 3; i++)
        {
            var s = _ringBuffer.Next();
            Thread.Sleep(100);

            _ringBuffer.Publish(s);
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
        var sequence = _ringBuffer.NewSequence();
        var sequenceBarrier = _ringBuffer.NewBarrier();
        var eventProcessor = CreateEventProcessor(_ringBuffer, sequence, sequenceBarrier, eventHandler);

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

        var sequence = _ringBuffer.NewSequence();
        var sequenceBarrier = _ringBuffer.NewBarrier();
        var eventProcessor = CreateEventProcessor(_ringBuffer, sequence, sequenceBarrier, eventHandler);
        _ringBuffer.SetGatingSequences(eventProcessor.SequencePointer);

        for (var i = 0; i < eventCountCount; i++)
        {
            _ringBuffer.PublishStubEvent(i);
        }

        eventProcessor.Start();

        Assert.That(eventSignal.Wait(TimeSpan.FromSeconds(2)));
        Assert.That(eventHandler.BatchSizes, Is.EqualTo(new List<long> { 6, 6, 3 }));

        eventProcessor.Halt();
    }

    public class LimitedBatchSizeEventHandler : IValueEventHandler<StubUnmanagedEvent>
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

        public void OnEvent(ref StubUnmanagedEvent data, long sequence, bool endOfBatch)
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
    public class BatchAwareEventHandler : IValueEventHandler<StubUnmanagedEvent>
    {
        public Dictionary<long, int> BatchSizeToCount { get; } = new();

        public void OnEvent(ref StubUnmanagedEvent data, long sequence, bool endOfBatch)
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

    public class ExplicitBatchStartImplementationEventHandler : IValueEventHandler<StubUnmanagedEvent>
    {
        private readonly CountdownEvent _signal;

        public int BatchCount { get; private set; }

        public ExplicitBatchStartImplementationEventHandler(CountdownEvent signal)
        {
            _signal = signal;
        }

        public void OnEvent(ref StubUnmanagedEvent data, long sequence, bool endOfBatch)
        {
            _signal.Signal();
        }

        void IValueEventHandler<StubUnmanagedEvent>.OnBatchStart(long batchSize)
        {
            ++BatchCount;
        }
    }
}
