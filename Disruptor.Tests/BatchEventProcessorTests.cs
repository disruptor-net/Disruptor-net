using System;
using System.Threading;
using Disruptor.Tests.Support;
using Moq;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class BatchEventProcessorTests
    {
        private RingBuffer<StubEvent> _ringBuffer;
        private ISequenceBarrier _sequenceBarrier;
        private Mock<IEventHandler<StubEvent>> _batchHandlerMock;
        private BatchEventProcessor<StubEvent> _batchEventProcessor;
        private CountdownEvent _countDownEvent;

        [SetUp]
        public void Setup()
        {
            _ringBuffer = new RingBuffer<StubEvent>(()=>new StubEvent(-1), 16);
            _sequenceBarrier = _ringBuffer.NewBarrier();
            _batchHandlerMock = new Mock<IEventHandler<StubEvent>>();
            _countDownEvent = new CountdownEvent(1);
            _batchEventProcessor = new BatchEventProcessor<StubEvent>(_ringBuffer, _sequenceBarrier, _batchHandlerMock.Object);
            _ringBuffer.SetGatingSequences(_batchEventProcessor.Sequence);
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ShouldThrowExceptionOnSettingNullExceptionHandler()
        {
            _batchEventProcessor.SetExceptionHandler(null);
        }

        [Test]
        public void ShouldCallMethodsInLifecycleOrder()
        {
            _batchHandlerMock.Setup(bh => bh.OnEvent(_ringBuffer[0], 0, true))
                             .Callback(() => _countDownEvent.Signal());

            var thread = new Thread(_batchEventProcessor.Run);
            thread.Start();

            Assert.AreEqual(-1L, _batchEventProcessor.Sequence.Value);

            _ringBuffer.Publish(_ringBuffer.Next());

            _countDownEvent.Wait(50);
            _batchEventProcessor.Halt();
            thread.Join();

            _batchHandlerMock.Verify(bh => bh.OnEvent(_ringBuffer[0], 0, true), Times.Once());
        }

        [Test]
        public void ShouldCallMethodsInLifecycleOrderForBatch()
        {
            _batchHandlerMock.Setup(bh => bh.OnEvent(_ringBuffer[0], 0, false));
            _batchHandlerMock.Setup(bh => bh.OnEvent(_ringBuffer[1], 1, false));
            _batchHandlerMock.Setup(bh => bh.OnEvent(_ringBuffer[2], 2, true)).Callback(() => _countDownEvent.Signal());

            _ringBuffer.Publish(_ringBuffer.Next());
            _ringBuffer.Publish(_ringBuffer.Next());
            _ringBuffer.Publish(_ringBuffer.Next());

            var thread = new Thread(_batchEventProcessor.Run);
            thread.Start();

            _countDownEvent.Wait(50);

            _batchEventProcessor.Halt();
            thread.Join();

            _batchHandlerMock.VerifyAll();
        }

        [Test]
        public void ShouldCallExceptionHandlerOnUncaughtException()
        {
            var ex = new Exception();
            var exceptionHandlerMock = new Mock<IExceptionHandler>();
            _batchEventProcessor.SetExceptionHandler(exceptionHandlerMock.Object);

            _batchHandlerMock.Setup(bh => bh.OnEvent(_ringBuffer[0], 0, true))
                             .Throws(ex); // OnNext raises an expcetion

            exceptionHandlerMock.Setup(bh => bh.HandleEventException(ex, 0, _ringBuffer[0]))
                             .Callback(() => _countDownEvent.Signal()); // Exception should be handled here and signal the CDE

            var thread = new Thread(_batchEventProcessor.Run);
            thread.Start();

            _ringBuffer.Publish(_ringBuffer.Next());

            _countDownEvent.Wait(50);

            _batchEventProcessor.Halt();
            thread.Join();

            _batchHandlerMock.VerifyAll();
            exceptionHandlerMock.VerifyAll();
        }

    }
}