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
    public class AsyncBatchEventProcessorTests
    {
        private readonly RingBuffer<StubEvent> _ringBuffer;
        private readonly IAsyncSequenceBarrier _sequenceBarrier;

        public AsyncBatchEventProcessorTests()
        {
            _ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(-1), new MultiProducerSequencer(16, new AsyncWaitStrategy()));
            _sequenceBarrier = (IAsyncSequenceBarrier)_ringBuffer.NewBarrier();
        }

        [Test]
        public void ShouldThrowExceptionOnSettingNullExceptionHandler()
        {
            var eventHandler = new TestAsyncBatchEventHandler<StubEvent>(x => throw new NullReferenceException());
            var eventProcessor = EventProcessorFactory.Create(_ringBuffer, _sequenceBarrier, eventHandler);

            Assert.Throws<ArgumentNullException>(() => eventProcessor.SetExceptionHandler(null!));
        }

        [Test]
        public void ShouldCallMethodsInLifecycleOrderForBatch()
        {
            var eventSignal = new CountdownEvent(3);
            var eventHandler = new TestAsyncBatchEventHandler<StubEvent>(x => eventSignal.Signal());
            var eventProcessor = EventProcessorFactory.Create(_ringBuffer, _sequenceBarrier, eventHandler);

            _ringBuffer.AddGatingSequences(eventProcessor.Sequence);

            _ringBuffer.Publish(_ringBuffer.Next());
            _ringBuffer.Publish(_ringBuffer.Next());
            _ringBuffer.Publish(_ringBuffer.Next());

            var task = eventProcessor.Start();

            Assert.IsTrue(eventSignal.Wait(TimeSpan.FromSeconds(2)));

            eventProcessor.Halt();

            Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(2)));
        }

        [Test]
        public void ShouldCallExceptionHandlerOnUncaughtException()
        {
            var exceptionSignal = new CountdownEvent(1);
            var exceptionHandler = new TestExceptionHandler<StubEvent>(x => exceptionSignal.Signal());
            var eventHandler = new TestAsyncBatchEventHandler<StubEvent>(x => throw new NullReferenceException());
            var eventProcessor = EventProcessorFactory.Create(_ringBuffer, _sequenceBarrier, eventHandler);
            _ringBuffer.AddGatingSequences(eventProcessor.Sequence);

            eventProcessor.SetExceptionHandler(exceptionHandler);

            var task = eventProcessor.Start();

            _ringBuffer.Publish(_ringBuffer.Next());

            Assert.IsTrue(exceptionSignal.Wait(TimeSpan.FromSeconds(2)));

            eventProcessor.Halt();

            Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(2)));
        }

        [Test]
        public void ShouldAlwaysHalt()
        {
            var waitStrategy = new AsyncWaitStrategy();
            var sequencer = new SingleProducerSequencer(8, waitStrategy);
            var barrier = (IAsyncSequenceBarrier)ProcessingSequenceBarrierFactory.Create(sequencer, waitStrategy, new Sequence(-1), new Sequence[0]);
            var dp = new ArrayDataProvider<object>(sequencer.BufferSize);

            var h1 = new LifeCycleHandler();
            var p1 = EventProcessorFactory.Create(dp, barrier, h1);

            p1.Halt();
            p1.Start();

            Assert.IsTrue(h1.WaitStart(TimeSpan.FromSeconds(2)));
            Assert.IsTrue(h1.WaitShutdown(TimeSpan.FromSeconds(2)));

            for (int i = 0; i < 1000; i++)
            {
                var h2 = new LifeCycleHandler();
                var p2 =  EventProcessorFactory.Create(dp, barrier, h2);
                p2.Start();

                p2.Halt();

                Assert.IsTrue(h2.WaitStart(TimeSpan.FromSeconds(2)));
                Assert.IsTrue(h2.WaitShutdown(TimeSpan.FromSeconds(2)));
            }

            for (int i = 0; i < 1000; i++)
            {
                var h2 = new LifeCycleHandler();
                var p2 =  EventProcessorFactory.Create(dp, barrier, h2);

                p2.Start();
                Thread.Yield();
                p2.Halt();

                Assert.IsTrue(h2.WaitStart(TimeSpan.FromSeconds(2)));
                Assert.IsTrue(h2.WaitShutdown(TimeSpan.FromSeconds(2)));
            }
        }

        private class LifeCycleHandler : IAsyncBatchEventHandler<object>, ILifecycleAware
        {
            private readonly ManualResetEvent _startedSignal = new ManualResetEvent(false);
            private readonly ManualResetEvent _shutdownSignal = new ManualResetEvent(false);

            public async ValueTask OnBatch(EventBatch<object> batch, long sequence)
            {
                await Task.Yield();
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
