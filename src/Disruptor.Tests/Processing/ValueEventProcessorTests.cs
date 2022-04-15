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
    private readonly ISequenceBarrier _sequenceBarrier;

    public ValueEventProcessorTests()
    {
        _ringBuffer = new ValueRingBuffer<StubValueEvent>(() => new StubValueEvent(-1), 16);
        _sequenceBarrier = _ringBuffer.NewBarrier();
    }

    private static IValueEventProcessor<T> CreateEventProcessor<T>(IValueDataProvider<T> dataProvider, ISequenceBarrier sequenceBarrier, IValueEventHandler<T> eventHandler)
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

        Assert.IsTrue(eventSignal.Wait(TimeSpan.FromSeconds(2)));

        eventProcessor.Halt();

        Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(2)));
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

        Assert.IsTrue(onTimeoutSignal.WaitOne(TimeSpan.FromSeconds(2)));

        eventProcessor.Halt();

        Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(2)));
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

        Assert.IsTrue(exceptionSignal.Wait(TimeSpan.FromSeconds(2)));
        Assert.AreEqual(0, exceptionHandler.EventExceptionCount);
        Assert.AreEqual(1, exceptionHandler.TimeoutExceptionCount);

        eventProcessor.Halt();

        Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(2)));
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

        Assert.IsTrue(exceptionSignal.Wait(TimeSpan.FromSeconds(2)));
        Assert.AreEqual(1, exceptionHandler.EventExceptionCount);
        Assert.AreEqual(0, exceptionHandler.TimeoutExceptionCount);

        eventProcessor.Halt();

        Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(2)));
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

        Assert.IsTrue(processingSignal.Wait(TimeSpan.FromSeconds(2)));
        Assert.AreEqual(2, exceptionHandler.EventExceptionCount);
        Assert.AreEqual(0, exceptionHandler.TimeoutExceptionCount);

        eventProcessor.Halt();

        Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(2)));
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
        Assert.IsTrue(signal.Wait(TimeSpan.FromSeconds(2)));

        eventProcessor.Halt();

        Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(2)));
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
        var barrier = ProcessingSequenceBarrierFactory.Create(sequencer, waitStrategy, new Sequence(-1), new Sequence[0]);
        var dp = new ArrayValueDataProvider<long>(sequencer.BufferSize);

        var h1 = new LifeCycleHandler();
        var p1 = CreateEventProcessor(dp, barrier, h1);

        p1.Halt();
        p1.Start();

        Assert.IsTrue(h1.WaitStart(TimeSpan.FromSeconds(2)));
        Assert.IsTrue(h1.WaitShutdown(TimeSpan.FromSeconds(2)));

        for (int i = 0; i < 1000; i++)
        {
            var h2 = new LifeCycleHandler();
            var p2 = CreateEventProcessor(dp, barrier, h2);
            p2.Start();

            p2.Halt();

            Assert.IsTrue(h2.WaitStart(TimeSpan.FromSeconds(2)));
            Assert.IsTrue(h2.WaitShutdown(TimeSpan.FromSeconds(2)));
        }

        for (int i = 0; i < 1000; i++)
        {
            var h2 = new LifeCycleHandler();
            var p2 = CreateEventProcessor(dp, barrier, h2);

            p2.Start();
            Thread.Yield();
            p2.Halt();

            Assert.IsTrue(h2.WaitStart(TimeSpan.FromSeconds(2)));
            Assert.IsTrue(h2.WaitShutdown(TimeSpan.FromSeconds(2)));
        }
    }

    private class LifeCycleHandler : IValueEventHandler<long>
    {
        private readonly ManualResetEvent _startedSignal = new(false);
        private readonly ManualResetEvent _shutdownSignal = new(false);

        public void OnEvent(ref long data, long sequence, bool endOfBatch)
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

        var eventProcessor = CreateEventProcessor(_ringBuffer, new DelegatingSequenceBarrier(_sequenceBarrier), eventHandler);

        _ringBuffer.AddGatingSequences(eventProcessor.Sequence);

        var task = eventProcessor.Start();

        for (var i = 0; i < 3; i++)
        {
            var sequence = _ringBuffer.Next();
            Thread.Sleep(100);

            _ringBuffer.Publish(sequence);
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
        Assert.IsTrue(signal.Wait(TimeSpan.FromSeconds(2)));

        eventProcessor.Halt();

        Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(2)));
        Assert.That(eventHandler.BatchCount, Is.EqualTo(1));
    }

    private class DelegatingSequenceBarrier : ISequenceBarrier
    {
        private readonly ISequenceBarrier _target;
        private bool _suppress = true;

        public DelegatingSequenceBarrier(ISequenceBarrier target)
        {
            _target = target;
        }

        public SequenceWaitResult WaitFor(long sequence)
        {
            var waitResult = _suppress ? new SequenceWaitResult(sequence - 1) : _target.WaitFor(sequence);
            _suppress = !_suppress;
            return waitResult;
        }

        public DependentSequenceGroup DependentSequences => _target.DependentSequences;

        public CancellationToken CancellationToken => _target.CancellationToken;

        public void ResetProcessing()
        {
            _target.ResetProcessing();
        }

        public void CancelProcessing()
        {
            _target.CancelProcessing();
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
