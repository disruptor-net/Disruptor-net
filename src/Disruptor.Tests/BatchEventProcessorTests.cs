using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture(BatchEventProcessorType.Legacy)]
    [TestFixture(BatchEventProcessorType.Optimized)]
    public class BatchEventProcessorTests
    {
        private readonly BatchEventProcessorType _targetType;
        private RingBuffer<StubEvent> _ringBuffer;
        private ISequenceBarrier _sequenceBarrier;

        public BatchEventProcessorTests(BatchEventProcessorType targetType)
        {
            _targetType = targetType;
        }

        [SetUp]
        public void Setup()
        {
            _ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(-1), 16);
            _sequenceBarrier = _ringBuffer.NewBarrier();
        }

        private IBatchEventProcessor<T> CreateBatchEventProcessor<T>(IDataProvider<T> dataProvider, ISequenceBarrier sequenceBarrier, IEventHandler<T> eventHandler)
            where T : class
        {
            switch (_targetType)
            {
                case BatchEventProcessorType.Legacy:
                    return new BatchEventProcessor<T>(dataProvider, sequenceBarrier, eventHandler);

                case BatchEventProcessorType.Optimized:
                    return BatchEventProcessorFactory.Create(dataProvider, sequenceBarrier, eventHandler);

                default:
                    throw new NotSupportedException();
            }
        }

        [Test]
        public void ShouldThrowExceptionOnSettingNullExceptionHandler()
        {
            var eventHandler = new TestEventHandler<StubEvent>(x => throw new NullReferenceException());
            var batchEventProcessor = CreateBatchEventProcessor(_ringBuffer, _sequenceBarrier, eventHandler);

            Assert.Throws<ArgumentNullException>(() => batchEventProcessor.SetExceptionHandler(null));
        }

        [Test]
        public void ShouldCallMethodsInLifecycleOrderForBatch()
        {
            var eventSignal = new CountdownEvent(3);
            var eventHandler = new TestEventHandler<StubEvent>(x => eventSignal.Signal());
            var batchEventProcessor = CreateBatchEventProcessor(_ringBuffer, _sequenceBarrier, eventHandler);

            _ringBuffer.AddGatingSequences(batchEventProcessor.Sequence);

            _ringBuffer.Publish(_ringBuffer.Next());
            _ringBuffer.Publish(_ringBuffer.Next());
            _ringBuffer.Publish(_ringBuffer.Next());

            var task = Task.Run(() => batchEventProcessor.Run());

            Assert.IsTrue(eventSignal.Wait(TimeSpan.FromSeconds(2)));

            batchEventProcessor.Halt();

            Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(2)));
        }

        [Test]
        public void ShouldCallExceptionHandlerOnUncaughtException()
        {
            var exceptionSignal = new CountdownEvent(1);
            var exceptionHandler = new TestExceptionHandler<StubEvent>(x => exceptionSignal.Signal());
            var eventHandler = new TestEventHandler<StubEvent>(x => throw new NullReferenceException());
            var batchEventProcessor = CreateBatchEventProcessor(_ringBuffer, _sequenceBarrier, eventHandler);
            _ringBuffer.AddGatingSequences(batchEventProcessor.Sequence);

            batchEventProcessor.SetExceptionHandler(exceptionHandler);

            var task = Task.Run(() => batchEventProcessor.Run());

            _ringBuffer.Publish(_ringBuffer.Next());

            Assert.IsTrue(exceptionSignal.Wait(TimeSpan.FromSeconds(2)));

            batchEventProcessor.Halt();

            Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(2)));
        }

        [Test]
        public void ReportAccurateBatchSizesAtBatchStartTime()
        {
            var batchSizes = new List<long>();
            var signal = new CountdownEvent(6);

            var batchEventProcessor = CreateBatchEventProcessor(_ringBuffer, _sequenceBarrier, new LoopbackEventHandler(_ringBuffer, batchSizes, signal));

            _ringBuffer.Publish(_ringBuffer.Next());
            _ringBuffer.Publish(_ringBuffer.Next());
            _ringBuffer.Publish(_ringBuffer.Next());

            var task = Task.Run(() => batchEventProcessor.Run());
            Assert.IsTrue(signal.Wait(TimeSpan.FromSeconds(2)));

            batchEventProcessor.Halt();

            Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(2)));
            Assert.That(batchSizes, Is.EqualTo(new List<long> { 3, 2, 1 }));
        }

        private class LoopbackEventHandler : IEventHandler<StubEvent>, IBatchStartAware
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
                    _ringBuffer.Publish(_ringBuffer.Next());
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
            var dp = new DummyDataProvider<object>();

            var h1 = new LifeCycleHandler();
            var p1 = CreateBatchEventProcessor(dp, barrier, h1);

            var t1 = new Thread(p1.Run);
            p1.Halt();
            t1.Start();

            Assert.IsTrue(h1.WaitStart(TimeSpan.FromSeconds(2)));
            Assert.IsTrue(h1.WaitShutdown(TimeSpan.FromSeconds(2)));

            for (int i = 0; i < 1000; i++)
            {
                var h2 = new LifeCycleHandler();
                var p2 = CreateBatchEventProcessor(dp, barrier, h2);
                var t2 = new Thread(p2.Run);

                t2.Start();
                p2.Halt();

                Assert.IsTrue(h2.WaitStart(TimeSpan.FromSeconds(2)));
                Assert.IsTrue(h2.WaitShutdown(TimeSpan.FromSeconds(2)));
            }

            for (int i = 0; i < 1000; i++)
            {
                var h2 = new LifeCycleHandler();
                var p2 = CreateBatchEventProcessor(dp, barrier, h2);

                var t2 = new Thread(p2.Run);
                t2.Start();
                Thread.Yield();
                p2.Halt();

                Assert.IsTrue(h2.WaitStart(TimeSpan.FromSeconds(2)));
                Assert.IsTrue(h2.WaitShutdown(TimeSpan.FromSeconds(2)));
            }
        }

        private class LifeCycleHandler : IEventHandler<object>, ILifecycleAware
        {
            private readonly ManualResetEvent _startedSignal = new ManualResetEvent(false);
            private readonly ManualResetEvent _shutdownSignal = new ManualResetEvent(false);

            public void OnEvent(object data, long sequence, bool endOfBatch)
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
            var latch = new CountdownEvent(3);

            var eventHandler = (BatchAwareEventHandler)Activator.CreateInstance(eventHandlerType, (Action<StubEvent>)(x => latch.Signal()));

            var batchEventProcessor = CreateBatchEventProcessor(_ringBuffer, new DelegatingSequenceBarrier(_sequenceBarrier), eventHandler);

            _ringBuffer.AddGatingSequences(batchEventProcessor.Sequence);

            var task = Task.Run(() => batchEventProcessor.Run());
            latch.Wait(TimeSpan.FromSeconds(2));

            _ringBuffer.Publish(_ringBuffer.Next());
            _ringBuffer.Publish(_ringBuffer.Next());
            _ringBuffer.Publish(_ringBuffer.Next());

            batchEventProcessor.Halt();
            task.Wait();

            Assert.That(eventHandler.BatchSizeToCount.Count, Is.Not.EqualTo(0));
            Assert.That(eventHandler.BatchSizeToCount.Keys, Has.No.Member(0));
        }

        private class DelegatingSequenceBarrier : ISequenceBarrier
        {
            private readonly ISequenceBarrier _target;
            private bool _suppress = true;

            public DelegatingSequenceBarrier(ISequenceBarrier target)
            {
                _target = target;
            }

            public long WaitFor(long sequence)
            {
                var result = _suppress ? sequence - 1 : _target.WaitFor(sequence);
                _suppress = !_suppress;
                return result;
            }

            public long Cursor => _target.Cursor;

            public bool IsAlerted => _target.IsAlerted;

            public void Alert()
            {
                _target.Alert();
            }

            public void ClearAlert()
            {
                _target.ClearAlert();
            }

            public void CheckAlert()
            {
                _target.CheckAlert();
            }
        }

        // ReSharper disable once MemberCanBePrivate.Global
        // Public to enable dynamic code generation
        public class BatchAwareEventHandler : TestEventHandler<StubEvent>, IBatchStartAware
        {
            public Dictionary<long, int> BatchSizeToCount { get; } = new Dictionary<long, int>();

            public BatchAwareEventHandler(Action<StubEvent> onEventAction)
                : base(onEventAction)
            {
            }

            public void OnBatchStart(long batchSize)
            {
                BatchSizeToCount[batchSize] = BatchSizeToCount.TryGetValue(batchSize, out var count) ? count + 1 : 1;
            }
        }

        internal class BatchAwareEventHandlerInternal : BatchAwareEventHandler
        {
            public BatchAwareEventHandlerInternal(Action<StubEvent> onEventAction)
                : base(onEventAction)
            {
            }
        }
    }
}
