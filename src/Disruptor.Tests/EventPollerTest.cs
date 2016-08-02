using System;
using Moq;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class EventPollerTest
    {
        [Test]
        public void ShouldPollForEvents()
        {
            var pollSequence = new Sequence();
            var bufferSequence = new Sequence();
            var gatingSequence = new Sequence();
            var sequencerMock = new Mock<ISequencer>();
            var sequencer = sequencerMock.Object;
            var handled = false;
            Func<object, long, bool, bool> handler = (ev, seq, end) =>
            {
                handled = true;
                return false;
            };
            var providerMock = new Mock<IDataProvider<object>>();
            var provider = providerMock.Object;
            var poller = EventPoller<object>.NewInstance(provider, sequencer, pollSequence, bufferSequence, gatingSequence);
            var @event = new object();

            object states = PollState.Idle;

            sequencerMock.SetupGet(x => x.Cursor)
                         .Returns(() =>
                         {
                             switch ((PollState)states)
                             {
                                 case PollState.Processing:
                                     return 0L;
                                 case PollState.Gating:
                                     return 0L;
                                 case PollState.Idle:
                                     return -1L;
                                 default:
                                     throw new ArgumentOutOfRangeException();
                             }
                         });

            sequencerMock.Setup(x => x.GetHighestPublishedSequence(0L, -1L)).Returns(-1L);
            sequencerMock.Setup(x => x.GetHighestPublishedSequence(0L, 0L)).Returns(0L);

            providerMock.Setup(x => x[0]).Returns(() => (PollState)states == PollState.Processing ? @event : null);
        
            // Initial State - nothing published.
            states = PollState.Idle;
            Assert.That(poller.Poll(handler),  Is.EqualTo(PollState.Idle));

            // Publish Event.
            states = PollState.Gating;
            bufferSequence.IncrementAndGet();
            Assert.That(poller.Poll(handler),  Is.EqualTo(PollState.Gating));

            states = PollState.Processing;
            gatingSequence.IncrementAndGet();
            Assert.That(poller.Poll(handler),  Is.EqualTo(PollState.Processing));

            Assert.That(handled, Is.True);
        }

        [Test]
        public void ShouldSuccessfullyPollWhenBufferIsFull()
        {
            var handled = 0;
            Func<byte[], long, bool, bool> handler = (ev, seq, end) =>
            {
                handled++;
                return true;
            };

            Func<byte[]> factory = () => new byte[1];

            var ringBuffer = RingBuffer<byte[]>.CreateMultiProducer(factory, 0x4, new SleepingWaitStrategy());

            var poller = ringBuffer.NewPoller();
            ringBuffer.AddGatingSequences(poller.Sequence);

            const int count = 4;

            for (byte i = 1; i <= count; ++i)
            {
                var next = ringBuffer.Next();
                ringBuffer[next][0] = i;
                ringBuffer.Publish(next);
            }

            // think of another thread
            poller.Poll(handler);

            Assert.That(handled, Is.EqualTo(4));
        }
    }
}
