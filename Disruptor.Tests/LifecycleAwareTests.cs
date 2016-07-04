using System.Threading;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class LifecycleAwareTests
    {
        private readonly ManualResetEvent _startMru = new ManualResetEvent(false);
        private readonly ManualResetEvent _shutdownMru = new ManualResetEvent(false);
        private readonly RingBuffer<StubEvent> _ringBuffer = new RingBuffer<StubEvent>(()=>new StubEvent(-1), 16);
        private ISequenceBarrier _sequenceBarrier;
        private LifecycleAwareEventHandler _eventHandler;
        private BatchEventProcessor<StubEvent> _batchEventProcessor;

        [SetUp]
        public void SetUp()
        {
            _sequenceBarrier = _ringBuffer.NewBarrier();
            _eventHandler = new LifecycleAwareEventHandler(_startMru, _shutdownMru);
            _batchEventProcessor = new BatchEventProcessor<StubEvent>(_ringBuffer, _sequenceBarrier, _eventHandler);
        }

        [Test]
        public void ShouldNotifyOfEventProcessorLifecycle()
        {
            new Thread(_batchEventProcessor.Run).Start();

            _startMru.WaitOne();

            _batchEventProcessor.Halt();

            _shutdownMru.WaitOne();

            Assert.AreEqual(_eventHandler.StartCounter, 1);
            Assert.AreEqual(_eventHandler.ShutdownCounter, 1);
        }
        
        private sealed class LifecycleAwareEventHandler : IEventHandler<StubEvent>, ILifecycleAware
        {
            private readonly ManualResetEvent _startMru;
            private readonly ManualResetEvent _shutdownMru;
            private int _startCounter;
            private int _shutdownCounter;

            public int StartCounter
            {
                get { return _startCounter; }
            }

            public int ShutdownCounter
            {
                get { return _shutdownCounter; }
            }

            public LifecycleAwareEventHandler(ManualResetEvent startMru, ManualResetEvent shutdownMru)
            {
                _startMru = startMru;
                _shutdownMru = shutdownMru;
            }

            public void OnEvent(StubEvent data, long sequence, bool endOfBatch)
            {
            }

            public void OnStart()
            {
                ++_startCounter;
                _startMru.Set();
            }

            public void OnShutdown()
            {
                ++_shutdownCounter;
                _shutdownMru.Set();
            }
        }
    }    
}
