using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;
using Disruptor.Tests.Support;
using NUnit.Framework;

#if DISRUPTOR_V5

namespace Disruptor.Tests.Processing
{
    [TestFixture]
    public class BatchEventProcessorTests
    {
        private readonly RingBuffer<StubEvent> _ringBuffer;
        private readonly ISequenceBarrier _sequenceBarrier;

        public BatchEventProcessorTests()
        {
            _ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(-1), 16);
            _sequenceBarrier = _ringBuffer.NewBarrier();
        }

        private static IEventProcessor<T> CreateEventProcessor<T>(IDataProvider<T> dataProvider, ISequenceBarrier sequenceBarrier, IBatchEventHandler<T> eventHandler)
            where T : class
        {
            return EventProcessorFactory.Create(dataProvider, sequenceBarrier, eventHandler);
        }

        [Test]
        public void ShouldThrowExceptionOnSettingNullExceptionHandler()
        {
            var eventHandler = new TestBatchEventHandler<StubEvent>(x => throw new NullReferenceException());
            var eventProcessor = CreateEventProcessor(_ringBuffer, _sequenceBarrier, eventHandler);

            Assert.Throws<ArgumentNullException>(() => eventProcessor.SetExceptionHandler(null!));
        }

        [Test]
        public void ShouldCallMethodsInLifecycleOrderForBatch()
        {
            var eventSignal = new CountdownEvent(3);
            var eventHandler = new TestBatchEventHandler<StubEvent>(x => eventSignal.Signal());
            var eventProcessor = CreateEventProcessor(_ringBuffer, _sequenceBarrier, eventHandler);

            _ringBuffer.AddGatingSequences(eventProcessor.Sequence);

            _ringBuffer.PublishStubEvent(0);
            _ringBuffer.PublishStubEvent(0);
            _ringBuffer.PublishStubEvent(0);

            var task = Task.Run(() => eventProcessor.Run());

            Assert.IsTrue(eventSignal.Wait(TimeSpan.FromSeconds(2)));

            eventProcessor.Halt();

            Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(2)));
        }

        [Test]
        public void ShouldCallExceptionHandlerOnUncaughtException()
        {
            var exceptionSignal = new CountdownEvent(1);
            var exceptionHandler = new TestExceptionHandler<StubEvent>(x => exceptionSignal.Signal());
            var eventHandler = new TestBatchEventHandler<StubEvent>(x => throw new NullReferenceException());
            var eventProcessor = CreateEventProcessor(_ringBuffer, _sequenceBarrier, eventHandler);
            _ringBuffer.AddGatingSequences(eventProcessor.Sequence);

            eventProcessor.SetExceptionHandler(exceptionHandler);

            var task = Task.Run(() => eventProcessor.Run());

            _ringBuffer.PublishStubEvent(0);

            Assert.IsTrue(exceptionSignal.Wait(TimeSpan.FromSeconds(2)));

            eventProcessor.Halt();

            Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(2)));
        }

        [Test]
        public void ShouldCallExceptionHandlerOnMultipleUncaughtException()
        {
            var processingSignal = new AutoResetEvent(false);
            var exceptionHandler = new TestExceptionHandler<StubEvent>(x => processingSignal.Set());
            var eventHandler = new TestBatchEventHandler<StubEvent>(x =>
            {
                if (x.Value == 1)
                    throw new Exception();

                processingSignal.Set();
            });
            var eventProcessor = CreateEventProcessor(_ringBuffer, _sequenceBarrier, eventHandler);
            _ringBuffer.AddGatingSequences(eventProcessor.Sequence);

            eventProcessor.SetExceptionHandler(exceptionHandler);

            var task = eventProcessor.Start();

            _ringBuffer.PublishStubEvent(0);
            Assert.IsTrue(processingSignal.WaitOne(TimeSpan.FromSeconds(2)));
            Assert.That(exceptionHandler.BatchExceptions, Has.Exactly(0).Items);

            _ringBuffer.PublishStubEvent(1);
            Assert.IsTrue(processingSignal.WaitOne(TimeSpan.FromSeconds(2)));
            Assert.That(exceptionHandler.BatchExceptions, Has.Exactly(1).Items);

            _ringBuffer.PublishStubEvent(0);
            Assert.IsTrue(processingSignal.WaitOne(TimeSpan.FromSeconds(2)));
            Assert.That(exceptionHandler.BatchExceptions, Has.Exactly(1).Items);

            _ringBuffer.PublishStubEvent(1);
            Assert.IsTrue(processingSignal.WaitOne(TimeSpan.FromSeconds(2)));
            Assert.That(exceptionHandler.BatchExceptions, Has.Exactly(2).Items);

            _ringBuffer.PublishStubEvent(0);
            Assert.IsTrue(processingSignal.WaitOne(TimeSpan.FromSeconds(2)));
            Assert.That(exceptionHandler.BatchExceptions, Has.Exactly(2).Items);

            eventProcessor.Halt();

            Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(2)));
        }

        [Test]
        public void ShouldAlwaysHalt()
        {
            var waitStrategy = new BusySpinWaitStrategy();
            var sequencer = new SingleProducerSequencer(8, waitStrategy);
            var barrier = ProcessingSequenceBarrierFactory.Create(sequencer, waitStrategy, new Sequence(-1), new Sequence[0]);
            var dp = new ArrayDataProvider<object>(sequencer.BufferSize);

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

        private class LifeCycleHandler : IBatchEventHandler<object>, ILifecycleAware
        {
            private readonly ManualResetEvent _startedSignal = new ManualResetEvent(false);
            private readonly ManualResetEvent _shutdownSignal = new ManualResetEvent(false);

            public void OnBatch(EventBatch<object> batch, long sequence)
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
    }
}

#endif
