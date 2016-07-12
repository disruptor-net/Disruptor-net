using System;
using System.Threading;
using System.Threading.Tasks;
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
            _ringBuffer = RingBuffer<StubEvent>.CreateMultiProducer(() => new StubEvent(-1), 64);

            _eventProcessorMock1 = new Mock<IEventProcessor>();
            _eventProcessorMock2 = new Mock<IEventProcessor>();
            _eventProcessorMock3 = new Mock<IEventProcessor>();

            _ringBuffer.AddGatingSequences(new NoOpEventProcessor<StubEvent>(_ringBuffer).Sequence);
        }

        [Test]
        public void ShouldWaitForWorkCompleteWhereCompleteWorkThresholdIsAhead()
        {
            const int expectedNumberMessages = 10;
            const int expectedWorkSequence = 9;
            FillRingBuffer(expectedNumberMessages);

            var sequence1 = new Sequence(expectedNumberMessages);
            var sequence2 = new Sequence(expectedWorkSequence);
            var sequence3 = new Sequence(expectedNumberMessages);

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
            const long expectedNumberMessages = 10;
            FillRingBuffer(expectedNumberMessages);

            var workers = new StubEventProcessor[3];
            for (var i = 0; i < workers.Length; i++)
            {
                workers[i] = new StubEventProcessor(expectedNumberMessages - 1);
            }

            var dependencyBarrier = _ringBuffer.NewBarrier(Util.GetSequencesFor(workers));

            new Thread(() =>
                    {
                        var sequence = _ringBuffer.Next();
                        _ringBuffer[sequence].Value = (int) sequence;
                        _ringBuffer.Publish(sequence);

                        foreach (var stubWorker in workers)
                        {
                            stubWorker.Sequence.SetValue(sequence);
                        }
                    })
                    .Start();

            const long expectedWorkSequence = expectedNumberMessages;
            var completedWorkSequence = dependencyBarrier.WaitFor(expectedNumberMessages);
            Assert.IsTrue(completedWorkSequence >= expectedWorkSequence);
        }

        [Test]
        public void ShouldInterruptDuringBusySpin()
        {
            const long expectedNumberMessages = 10;
            FillRingBuffer(expectedNumberMessages);

            var signal = new CountdownEvent(3);
            var sequence1 = new CountDownEventSequence(8L, signal);
            var sequence2 = new CountDownEventSequence(8L, signal);
            var sequence3 = new CountDownEventSequence(8L, signal);

            _eventProcessorMock1.Setup(x => x.Sequence).Returns(sequence1);
            _eventProcessorMock2.Setup(x => x.Sequence).Returns(sequence2);
            _eventProcessorMock3.Setup(x => x.Sequence).Returns(sequence3);

            var sequenceBarrier = _ringBuffer.NewBarrier(Util.GetSequencesFor(_eventProcessorMock1.Object, _eventProcessorMock2.Object, _eventProcessorMock3.Object));

            var alerted = false;
            var t = Task.Factory.StartNew(() =>
                                  {
                                      try
                                      {
                                          sequenceBarrier.WaitFor(expectedNumberMessages - 1);
                                      }
                                      catch (AlertException)
                                      {
                                          alerted = true;
                                      }
                                  });

            signal.Wait(TimeSpan.FromSeconds(3));
            sequenceBarrier.Alert();
            t.Wait();

            Assert.That(alerted, Is.True, "Thread was not interrupted");
        }

        [Test]
        public void ShouldWaitForWorkCompleteWhereCompleteWorkThresholdIsBehind()
        {
            const long expectedNumberMessages = 10;
            FillRingBuffer(expectedNumberMessages);

            var eventProcessors = new StubEventProcessor[3];
            for (var i = 0; i < eventProcessors.Length; i++)
            {
                eventProcessors[i] = new StubEventProcessor(expectedNumberMessages - 2);
            }

            var eventProcessorBarrier = _ringBuffer.NewBarrier(Util.GetSequencesFor(eventProcessors));

            Task.Factory.StartNew(() =>
                                  {
                                      foreach (var stubWorker in eventProcessors)
                                      {
                                          stubWorker.Sequence.SetValue(stubWorker.Sequence.Value + 1);
                                      }
                                  }).Wait();

            const long expectedWorkSequence = expectedNumberMessages - 1;
            var completedWorkSequence = eventProcessorBarrier.WaitFor(expectedWorkSequence);
            Assert.IsTrue(completedWorkSequence >= expectedWorkSequence);
        }

        [Test]
        public void ShouldSetAndClearAlertStatus()
        {
            var sequenceBarrier = _ringBuffer.NewBarrier();
            Assert.IsFalse(sequenceBarrier.IsAlerted);

            sequenceBarrier.Alert();
            Assert.IsTrue(sequenceBarrier.IsAlerted);

            sequenceBarrier.ClearAlert();
            Assert.IsFalse(sequenceBarrier.IsAlerted);
        }

        private void FillRingBuffer(long expectedNumberEvents)
        {
            for (var i = 0; i < expectedNumberEvents; i++)
            {
                var sequence = _ringBuffer.Next();
                var @event = _ringBuffer[sequence];
                @event.Value = i;
                _ringBuffer.Publish(sequence);
            }
        }

        private class StubEventProcessor : IEventProcessor
        {
            private volatile int _running;
            private readonly Sequence _sequence = new Sequence();

            public StubEventProcessor(long sequence)
            {
                _sequence.SetValue(sequence);
            }

            public void Run()
            {
                if(Interlocked.Exchange(ref _running, 1) != 0)
                    throw new InvalidOperationException("Already running");
            }

            public bool IsRunning => _running == 1;

            public ISequence Sequence => _sequence;

            public void Halt()
            {
                _running = 0;
            }
        }

        private class CountDownEventSequence : ISequence
        {
            private readonly CountdownEvent _signal;
            private readonly ISequence _sequenceImplementation;

            public CountDownEventSequence(long initialValue, CountdownEvent signal)
            {
                _sequenceImplementation = new Sequence(initialValue);
                _signal = signal;
            }

            public long Value
            {
                get
                {
                    if (_signal.CurrentCount > 0)
                        _signal.Signal();

                    return _sequenceImplementation.Value;
                }
            }

            public void SetValue(long value)
            {
                _sequenceImplementation.SetValue(value);
            }

            public void SetValueVolatile(long value)
            {
                _sequenceImplementation.SetValueVolatile(value);
            }

            public bool CompareAndSet(long expectedSequence, long nextSequence)
            {
                return _sequenceImplementation.CompareAndSet(expectedSequence, nextSequence);
            }

            public long IncrementAndGet()
            {
                return _sequenceImplementation.IncrementAndGet();
            }

            public long AddAndGet(long value)
            {
                return _sequenceImplementation.AddAndGet(value);
            }
        }
    }
}