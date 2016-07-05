using System;
using System.Threading;
using Disruptor.Tests.Support;
using Moq;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class SequenceBarrierTests
    {
        private RingBuffer<StubEvent> _ringBuffer;
        private Mock<IEventProcessor> _eventProcessorMock1;
        private Mock<IEventProcessor> _eventProcessorMock2;
        private Mock<IEventProcessor> _eventProcessorMock3;

        [SetUp]
        public void SetUp()
        {
            _ringBuffer = new RingBuffer<StubEvent>(()=>new StubEvent(-1), 64);

            _eventProcessorMock1 = new Mock<IEventProcessor>();
            _eventProcessorMock2 = new Mock<IEventProcessor>();
            _eventProcessorMock3 = new Mock<IEventProcessor>();

            _ringBuffer.SetGatingSequences(new NoOpEventProcessor(_ringBuffer).Sequence);
        }

        [Test]
        public void ShouldWaitForWorkCompleteWhereCompleteWorkThresholdIsAhead()
        {
            const int expectedNumberEvents = 10;
            const int expectedWorkSequence = 9;
            FillRingBuffer(expectedNumberEvents);

            var sequence1 = new Sequence(expectedNumberEvents);
            var sequence2 = new Sequence(expectedWorkSequence);
            var sequence3 = new Sequence(expectedNumberEvents);

            _eventProcessorMock1.SetupGet(c => c.Sequence).Returns(sequence1);
            _eventProcessorMock2.SetupGet(c => c.Sequence).Returns(sequence2);
            _eventProcessorMock3.SetupGet(c => c.Sequence).Returns(sequence3);

            var dependencyBarrier = _ringBuffer.NewBarrier(_eventProcessorMock1.Object.Sequence, 
                                                           _eventProcessorMock2.Object.Sequence, 
                                                           _eventProcessorMock3.Object.Sequence);

            var completedWorkSequence = dependencyBarrier.WaitFor(expectedWorkSequence);
            Assert.IsTrue(completedWorkSequence >= expectedWorkSequence);

            _eventProcessorMock1.Verify();
            _eventProcessorMock2.Verify();
            _eventProcessorMock3.Verify();
        }

        [Test]
        public void ShouldWaitForWorkCompleteWhereAllWorkersAreBlockedOnRingBuffer()
        {
            const long expectedNumberEvents = 10;
            FillRingBuffer(expectedNumberEvents);

            var workers = new StubEventProcessor[3];
            for (var i = 0; i < workers.Length; i++)
            {
                workers[i] = new StubEventProcessor(expectedNumberEvents - 1);
            }

            var dependencyBarrier = _ringBuffer.NewBarrier(Util.GetSequencesFor(workers));

            new Thread(() =>
                    {
                        var sequence = _ringBuffer.Next();
                        _ringBuffer[sequence].Value = (int) sequence;
                        _ringBuffer.Publish(sequence);

                        foreach (var stubWorker in workers)
                        {
                            stubWorker.Sequence.Value = sequence;
                        }
                    })
                    .Start();

            const long expectedWorkSequence = expectedNumberEvents;
            var completedWorkSequence = dependencyBarrier.WaitFor(expectedNumberEvents);
            Assert.IsTrue(completedWorkSequence >= expectedWorkSequence);
        }

        [Test]
        public void ShouldInterruptDuringBusySpin()
        {
            const long expectedNumberEvents = 10;
            FillRingBuffer(expectedNumberEvents);

            var sequence1 = new Sequence(8L);
            var sequence2 = new Sequence(8L);
            var sequence3 = new Sequence(8L);

            _eventProcessorMock1.SetupGet(c => c.Sequence).Returns(sequence1);
            _eventProcessorMock2.SetupGet(c => c.Sequence).Returns(sequence2);
            _eventProcessorMock3.SetupGet(c => c.Sequence).Returns(sequence3);

            var dependencyBarrier = _ringBuffer.NewBarrier(_eventProcessorMock1.Object.Sequence, 
                                                           _eventProcessorMock2.Object.Sequence, 
                                                           _eventProcessorMock3.Object.Sequence);
                    
            var alerted = new[] { false };
            var t = new Thread(
                () =>
                    {
                        try
                        {
                            dependencyBarrier.WaitFor(expectedNumberEvents - 1);
                        }
                        catch (AlertException)
                        {
                            alerted[0] = true;
                        }
                    });

            t.Start();
            Thread.Sleep(TimeSpan.FromSeconds(1));
            dependencyBarrier.Alert();
            t.Join();

            Assert.IsTrue(alerted[0], "Thread was not interrupted");

            _eventProcessorMock1.Verify();
            _eventProcessorMock2.Verify();
            _eventProcessorMock3.Verify();
        }

        [Test]
        public void ShouldWaitForWorkCompleteWhereCompleteWorkThresholdIsBehind()
        {
            const long expectedNumberEvents = 10;
            FillRingBuffer(expectedNumberEvents);

            var eventProcessors = new StubEventProcessor[3];
            for (var i = 0; i < eventProcessors.Length; i++)
            {
                eventProcessors[i] = new StubEventProcessor(expectedNumberEvents - 2);
            }

            var eventProcessorBarrier = _ringBuffer.NewBarrier(Util.GetSequencesFor(eventProcessors));

            new Thread(() =>
                           {
                               foreach (var stubWorker in eventProcessors)
                               {
                                   stubWorker.Sequence.Value += 1;
                               }
                           }).Start();

            const long expectedWorkSequence = expectedNumberEvents - 1;
            long completedWorkSequence = eventProcessorBarrier.WaitFor(expectedWorkSequence);
            Assert.IsTrue(completedWorkSequence >= expectedWorkSequence);
        }

        [Test]
        public void ShouldSetAndClearAlertStatus()
        {
            var dependencyBarrier = _ringBuffer.NewBarrier();
            Assert.IsFalse(dependencyBarrier.IsAlerted);

            dependencyBarrier.Alert();
            Assert.IsTrue(dependencyBarrier.IsAlerted);

            dependencyBarrier.ClearAlert();
            Assert.IsFalse(dependencyBarrier.IsAlerted);
        }

        private void FillRingBuffer(long expectedNumberEvents)
        {
            for (var i = 0; i < expectedNumberEvents; i++)
            {
                var sequence = _ringBuffer.Next();
                _ringBuffer[sequence].Value = i;
                _ringBuffer.Publish(sequence);
            }
        }

        private class StubEventProcessor : IEventProcessor
        {
            private readonly Sequence _sequence = new Sequence(Sequence.InitialCursorValue);

            public StubEventProcessor(long sequence)
            {
                _sequence.Value = sequence;
            }

            public void Run()
            {
                IsRunning = true;
            }

            public bool IsRunning { get; private set; }

            public Sequence Sequence
            {
                get { return _sequence; }
            }

            public void Halt()
            {
                IsRunning = false;
            }
        }
    }
}