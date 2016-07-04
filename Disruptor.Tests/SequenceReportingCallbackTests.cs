using System.Threading;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class SequenceReportingCallbackTests
    {
        private readonly ManualResetEvent _callbackLatch = new ManualResetEvent(false);
        private readonly ManualResetEvent _onEndOfBatchLatch = new ManualResetEvent(false);

        [Test]
        public void ShouldReportProgressByUpdatingSequenceViaCallback()
        {
            var ringBuffer = new RingBuffer<StubEvent>(()=>new StubEvent(0), 16);
            var sequenceBarrier = ringBuffer.NewBarrier();
            var handler = new TestSequenceReportingEventHandler(_callbackLatch);
            var batchEventProcessor = new BatchEventProcessor<StubEvent>(ringBuffer, sequenceBarrier, handler);
            ringBuffer.SetGatingSequences(batchEventProcessor.Sequence);

            var thread = new Thread(batchEventProcessor.Run) {IsBackground = true};
            thread.Start();

            Assert.AreEqual(-1L, batchEventProcessor.Sequence.Value);
            ringBuffer.Publish(ringBuffer.Next());

            _callbackLatch.WaitOne();
            Assert.AreEqual(0L, batchEventProcessor.Sequence.Value);

            _onEndOfBatchLatch.Set();
            Assert.AreEqual(0L, batchEventProcessor.Sequence.Value);

            batchEventProcessor.Halt();
            thread.Join();
        }

        private class TestSequenceReportingEventHandler : ISequenceReportingEventHandler<StubEvent>
        {
            private Sequence _sequenceCallback;
            private readonly ManualResetEvent _callbackLatch;

            public TestSequenceReportingEventHandler(ManualResetEvent callbackLatch)
            {
                _callbackLatch = callbackLatch;
            }

            public void SetSequenceCallback(Sequence sequenceTrackerCallback)
            {
                _sequenceCallback = sequenceTrackerCallback;
            }

            public void OnEvent(StubEvent evt, long sequence, bool endOfBatch)
            {
                _sequenceCallback.Value = sequence;
                _callbackLatch.Set();
            }
        }
    }
}