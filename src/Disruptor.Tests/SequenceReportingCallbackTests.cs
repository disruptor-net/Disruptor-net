using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class SequenceReportingCallbackTests
    {
        [Test]
        public void ShouldReportEventHandlerProgressByUpdatingSequenceViaCallback()
        {
            var ringBuffer = RingBuffer<StubEvent>.CreateMultiProducer(() => new StubEvent(-1), 16);
            var sequenceBarrier = ringBuffer.NewBarrier();
            var handler = new TestSequenceReportingEventHandler();
            var eventProcessor = EventProcessorFactory.Create(ringBuffer, sequenceBarrier, handler);
            ringBuffer.AddGatingSequences(eventProcessor.Sequence);

            var task = eventProcessor.Start();

            Assert.AreEqual(-1L, eventProcessor.Sequence.Value);
            ringBuffer.Publish(ringBuffer.Next());

            handler.CallbackSignal.WaitOne();
            Assert.AreEqual(0L, eventProcessor.Sequence.Value);

            handler.OnEndOfBatchSignal.Set();
            Assert.AreEqual(0L, eventProcessor.Sequence.Value);

            eventProcessor.Halt();
            Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(10)));
        }

        [Test]
        public void ShouldReportValueEventHandlerProgressByUpdatingSequenceViaCallback()
        {
            var ringBuffer = ValueRingBuffer<StubValueEvent>.CreateMultiProducer(() => new StubValueEvent(-1), 16);
            var sequenceBarrier = ringBuffer.NewBarrier();
            var handler = new TestSequenceReportingEventHandler();
            var eventProcessor = EventProcessorFactory.Create(ringBuffer, sequenceBarrier, handler);
            ringBuffer.AddGatingSequences(eventProcessor.Sequence);

            var task = eventProcessor.Start();

            Assert.AreEqual(-1L, eventProcessor.Sequence.Value);
            ringBuffer.Publish(ringBuffer.Next());

            handler.CallbackSignal.WaitOne();
            Assert.AreEqual(0L, eventProcessor.Sequence.Value);

            handler.OnEndOfBatchSignal.Set();
            Assert.AreEqual(0L, eventProcessor.Sequence.Value);

            eventProcessor.Halt();
            Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(10)));
        }

        private class TestSequenceReportingEventHandler : IEventHandler<StubEvent>, IValueEventHandler<StubValueEvent>, IEventProcessorSequenceAware
        {
            private ISequence? _sequenceCallback;

            public ManualResetEvent CallbackSignal { get; } = new(false);
            public ManualResetEvent OnEndOfBatchSignal { get; } = new(false);

            public void SetSequenceCallback(ISequence sequenceTrackerCallback)
            {
                _sequenceCallback = sequenceTrackerCallback;
            }

            public void OnEvent(ref StubValueEvent data, long sequence, bool endOfBatch)
            {
                OnEvent(sequence, endOfBatch);
            }

            public void OnEvent(StubEvent evt, long sequence, bool endOfBatch)
            {
                OnEvent(sequence, endOfBatch);
            }

            private void OnEvent(long sequence, bool endOfBatch)
            {
                _sequenceCallback!.SetValue(sequence);
                CallbackSignal.Set();

                if (endOfBatch)
                {
                    OnEndOfBatchSignal.WaitOne();
                }
            }
        }
    }
}
