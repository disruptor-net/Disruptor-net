using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;
using Disruptor.Tests.Support;
using NUnit.Framework;

#if BATCH_HANDLER

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

        [Test]
        public void ShouldThrowExceptionOnSettingNullExceptionHandler()
        {
            var eventHandler = new TestBatchEventHandler<StubEvent>(x => throw new NullReferenceException());
            var eventProcessor = EventProcessorFactory.Create(_ringBuffer, _sequenceBarrier, eventHandler);

            Assert.Throws<ArgumentNullException>(() => eventProcessor.SetExceptionHandler(null));
        }

        [Test]
        public void ShouldCallMethodsInLifecycleOrderForBatch()
        {
            var eventSignal = new CountdownEvent(3);
            var eventHandler = new TestBatchEventHandler<StubEvent>(x => eventSignal.Signal());
            var eventProcessor = EventProcessorFactory.Create(_ringBuffer, _sequenceBarrier, eventHandler);

            _ringBuffer.AddGatingSequences(eventProcessor.Sequence);

            _ringBuffer.Publish(_ringBuffer.Next());
            _ringBuffer.Publish(_ringBuffer.Next());
            _ringBuffer.Publish(_ringBuffer.Next());

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
            var eventProcessor = EventProcessorFactory.Create(_ringBuffer, _sequenceBarrier, eventHandler);
            _ringBuffer.AddGatingSequences(eventProcessor.Sequence);

            eventProcessor.SetExceptionHandler(exceptionHandler);

            var task = Task.Run(() => eventProcessor.Run());

            _ringBuffer.Publish(_ringBuffer.Next());

            Assert.IsTrue(exceptionSignal.Wait(TimeSpan.FromSeconds(2)));

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
            var p1 = EventProcessorFactory.Create(dp, barrier, h1);

            var t1 = Task.Run(p1.Run);
            p1.Halt();

            AssertIsStartedAndShutdown(h1, t1);;

            for (int i = 0; i < 1000; i++)
            {
                var h2 = new LifeCycleHandler();
                var p2 = EventProcessorFactory.Create(dp, barrier, h2);

                var t2 = Task.Run(p2.Run);
                p2.Halt();

                AssertIsStartedAndShutdown(h2, t2);
            }

            for (int i = 0; i < 1000; i++)
            {
                var h2 = new LifeCycleHandler();
                var p2 = EventProcessorFactory.Create(dp, barrier, h2);

                var t2 = Task.Run(p2.Run);

                Thread.Yield();
                p2.Halt();

                AssertIsStartedAndShutdown(h2, t2);
            }

            for (int i = 0; i < 1000; i++)
            {
                var h2 = new LifeCycleHandler();
                var p2 = EventProcessorFactory.Create(dp, barrier, h2);

                var t2 = Task.Run(() =>
                {
                    Thread.Yield();
                    p2.Run();
                });

                p2.Halt();

                AssertIsStartedAndShutdown(h2, t2);
            }

            static void AssertIsStartedAndShutdown(LifeCycleHandler handler, Task task)
            {
                var waitTimeout = TimeSpan.FromSeconds(5); // High timeout to

                Assert.IsTrue(handler.WaitStart(waitTimeout));
                Assert.IsTrue(handler.WaitShutdown(waitTimeout));
                Assert.IsTrue(task.Wait(waitTimeout));
            }
        }

        private class LifeCycleHandler : IBatchEventHandler<object>, ILifecycleAware
        {
            private readonly ManualResetEvent _startedSignal = new ManualResetEvent(false);
            private readonly ManualResetEvent _shutdownSignal = new ManualResetEvent(false);

            public void OnBatch(ReadOnlySpan<object> batch, long sequence)
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
