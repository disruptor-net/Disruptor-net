using System.Threading;
using Disruptor.Processing;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class LifecycleAwareTests
    {
        private readonly ManualResetEvent _startSignal = new ManualResetEvent(false);
        private readonly ManualResetEvent _shutdownSignal = new ManualResetEvent(false);
        private readonly RingBuffer<StubEvent> _ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(-1), 16);
        private readonly LifecycleAwareEventHandler _eventHandler;
        private readonly IEventProcessor<StubEvent> _eventProcessor;

        public LifecycleAwareTests()
        {
            var sequenceBarrier = _ringBuffer.NewBarrier();
            _eventHandler = new LifecycleAwareEventHandler(_startSignal, _shutdownSignal);
            _eventProcessor = EventProcessorFactory.Create(_ringBuffer, sequenceBarrier, _eventHandler);
        }

        [Test]
        public void ShouldNotifyOfBatchProcessorLifecycle()
        {
            _eventProcessor.Start();

            _startSignal.WaitOne();
            _eventProcessor.Halt();

            _shutdownSignal.WaitOne();

            Assert.AreEqual(_eventHandler.StartCounter, 1);
            Assert.AreEqual(_eventHandler.ShutdownCounter, 1);
        }

        private sealed class LifecycleAwareEventHandler : IEventHandler<StubEvent>, ILifecycleAware
        {
            private readonly ManualResetEvent _startSignal;
            private readonly ManualResetEvent _shutdownSignal;

            public int StartCounter { get; private set; }

            public int ShutdownCounter { get; private set; }

            public LifecycleAwareEventHandler(ManualResetEvent startSignal, ManualResetEvent shutdownSignal)
            {
                _startSignal = startSignal;
                _shutdownSignal = shutdownSignal;
            }

            public void OnEvent(StubEvent data, long sequence, bool endOfBatch)
            {
            }

            public void OnStart()
            {
                ++StartCounter;
                _startSignal.Set();
            }

            public void OnShutdown()
            {
                ++ShutdownCounter;
                _shutdownSignal.Set();
            }
        }
    }
}
