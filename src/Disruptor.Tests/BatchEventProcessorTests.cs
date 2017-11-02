using System;
using System.Threading;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class BatchEventProcessorTests
    {
        private RingBuffer<StubEvent> _ringBuffer;
        private ISequenceBarrier _sequenceBarrier;

        [SetUp]
        public void Setup()
        {
            _ringBuffer = new RingBuffer<StubEvent>(()=>new StubEvent(-1), 16);
            _sequenceBarrier = _ringBuffer.NewBarrier();
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ShouldThrowExceptionOnSettingNullExceptionHandler()
        {
            var eventHandler = new ActionEventHandler<StubEvent>(x => throw new NullReferenceException());
            var batchEventProcessor = new BatchEventProcessor<StubEvent>(_ringBuffer, _sequenceBarrier, eventHandler);
            batchEventProcessor.SetExceptionHandler(null);
        }

        [Test]
        public void ShouldCallMethodsInLifecycleOrderForBatch()
        {
            var eventSignal = new CountdownEvent(3);
            var eventHandler = new ActionEventHandler<StubEvent>(x => eventSignal.Signal());
            var batchEventProcessor = new BatchEventProcessor<StubEvent>(_ringBuffer, _sequenceBarrier, eventHandler);

            _ringBuffer.AddGatingSequences(batchEventProcessor.Sequence);

            _ringBuffer.Publish(_ringBuffer.Next());
            _ringBuffer.Publish(_ringBuffer.Next());
            _ringBuffer.Publish(_ringBuffer.Next());

            var thread = new Thread(batchEventProcessor.Run);
            thread.Start();

            Assert.IsTrue(eventSignal.Wait(TimeSpan.FromSeconds(2)));

            batchEventProcessor.Halt();
            thread.Join();
        }

        [Test]
        public void ShouldCallExceptionHandlerOnUncaughtException()
        {
            var exceptionSignal = new CountdownEvent(1);
            var exceptionHandler = new ActionExceptionHandler<StubEvent>(x => exceptionSignal.Signal());
            var eventHandler = new ActionEventHandler<StubEvent>(x => throw new NullReferenceException());
            var batchEventProcessor = new BatchEventProcessor<StubEvent>(_ringBuffer, _sequenceBarrier, eventHandler);
            _ringBuffer.AddGatingSequences(batchEventProcessor.Sequence);

            batchEventProcessor.SetExceptionHandler(exceptionHandler);

            Thread thread = new Thread(batchEventProcessor.Run);
            thread.Start();

            _ringBuffer.Publish(_ringBuffer.Next());

            Assert.IsTrue(exceptionSignal.Wait(TimeSpan.FromSeconds(2)));

            batchEventProcessor.Halt();
            thread.Join();
        }
    }
}
