using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
            _ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(-1), 16);
            _sequenceBarrier = _ringBuffer.NewBarrier();
        }

        [Test]
        public void ShouldThrowExceptionOnSettingNullExceptionHandler()
        {
            var eventHandler = new ActionEventHandler<StubEvent>(x => throw new NullReferenceException());
            var batchEventProcessor = new BatchEventProcessor<StubEvent>(_ringBuffer, _sequenceBarrier, eventHandler);

            Assert.Throws<ArgumentNullException>(() => batchEventProcessor.SetExceptionHandler(null));
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

            var task = Task.Run(() => batchEventProcessor.Run());

            Assert.IsTrue(eventSignal.Wait(TimeSpan.FromSeconds(2)));

            batchEventProcessor.Halt();

            Assert.IsTrue(task.Wait(500));
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

            var task = Task.Run(() => batchEventProcessor.Run());

            _ringBuffer.Publish(_ringBuffer.Next());

            Assert.IsTrue(exceptionSignal.Wait(TimeSpan.FromSeconds(2)));

            batchEventProcessor.Halt();

            Assert.IsTrue(task.Wait(500));
        }

        [Test]
        public void ReportAccurateBatchSizesAtBatchStartTime()
        {
            var batchSizes = new List<long>();
            var signal = new CountdownEvent(6);

            var batchEventProcessor = new BatchEventProcessor<StubEvent>(_ringBuffer, _sequenceBarrier, new LoopbackEventHandler(_ringBuffer, batchSizes, signal));

            _ringBuffer.Publish(_ringBuffer.Next());
            _ringBuffer.Publish(_ringBuffer.Next());
            _ringBuffer.Publish(_ringBuffer.Next());

            var task = Task.Run(() => batchEventProcessor.Run());
            signal.Wait();

            batchEventProcessor.Halt();

            Assert.IsTrue(task.Wait(500));
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
    }
}
