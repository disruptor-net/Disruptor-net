using System.Threading;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class SequenceReportingCallbackTests
    {
        private readonly ManualResetEvent _callbackSignal = new ManualResetEvent(false);
        private readonly ManualResetEvent _onEndOfBatchSignal = new ManualResetEvent(false);

        [Test]
        public void ShouldReportProgressByUpdatingSequenceViaCallback()
        {
            var ringBuffer = RingBuffer<StubEvent>.CreateMultiProducer(() => new StubEvent(-1), 16);
            var sequenceBarrier = ringBuffer.NewBarrier();
            ISequenceReportingEventHandler<StubEvent> handler = new TestSequenceReportingEventHandler(_callbackSignal, _onEndOfBatchSignal);
            var batchEventProcessor = BatchEventProcessorFactory.Create(ringBuffer, sequenceBarrier, handler);
            ringBuffer.AddGatingSequences(batchEventProcessor.Sequence);

            var thread = new Thread(batchEventProcessor.Run) {IsBackground = true};
            thread.Start();

            Assert.AreEqual(-1L, batchEventProcessor.Sequence.Value);
            ringBuffer.Publish(ringBuffer.Next());

            _callbackSignal.WaitOne();
            Assert.AreEqual(0L, batchEventProcessor.Sequence.Value);

            _onEndOfBatchSignal.Set();
            Assert.AreEqual(0L, batchEventProcessor.Sequence.Value);

            batchEventProcessor.Halt();
            thread.Join();
        }

        private class TestSequenceReportingEventHandler : ISequenceReportingEventHandler<StubEvent>
        {
            private ISequence _sequenceCallback;
            private readonly ManualResetEvent _callbackSignal;
            private readonly ManualResetEvent _onEndOfBatchSignal;

            public TestSequenceReportingEventHandler(ManualResetEvent callbackSignal, ManualResetEvent onEndOfBatchSignal)
            {
                _callbackSignal = callbackSignal;
                _onEndOfBatchSignal = onEndOfBatchSignal;
            }

            public void SetSequenceCallback(ISequence sequenceTrackerCallback)
            {
                _sequenceCallback = sequenceTrackerCallback;
            }

            public void OnEvent(StubEvent evt, long sequence, bool endOfBatch)
            {
                _sequenceCallback.SetValue(sequence);
                _callbackSignal.Set();

                if (endOfBatch)
                {
                    _onEndOfBatchSignal.WaitOne();
                }
            }
        }
    }
}
