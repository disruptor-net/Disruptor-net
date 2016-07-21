using System;
using Disruptor.Dsl;
using Disruptor.Tests.Dsl.Stubs;
using Disruptor.Tests.Support;
using Moq;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class ConsumerRepositoryTests
    {
        private ConsumerRepository<TestEvent> _consumerRepository;
        private Mock<IEventProcessor> _eventProcessor1;
        private Mock<IEventProcessor> _eventProcessor2;
        private SleepingEventHandler _handler1;
        private SleepingEventHandler _handler2;
        private Mock<ISequenceBarrier> _barrier1;
        private Mock<ISequenceBarrier> _barrier2;

        [SetUp]
        public void SetUp()
        {
            _consumerRepository = new ConsumerRepository<TestEvent>();
            _eventProcessor1 = new Mock<IEventProcessor>();
            _eventProcessor2 = new Mock<IEventProcessor>();

            var sequence1 = new Sequence();
            var sequence2 = new Sequence();

            _eventProcessor1.Setup(x => x.Sequence).Returns(sequence1);
            _eventProcessor1.Setup(x => x.IsRunning).Returns(true);
            _eventProcessor2.Setup(x => x.Sequence).Returns(sequence2);
            _eventProcessor2.Setup(x => x.IsRunning).Returns(true);

            _handler1 = new SleepingEventHandler();
            _handler2 = new SleepingEventHandler();

            _barrier1 = new Mock<ISequenceBarrier>();
            _barrier2 = new Mock<ISequenceBarrier>();
        }

        [Test]
        public void ShouldGetBarrierByHandler()
        {
            _consumerRepository.Add(_eventProcessor1.Object, _handler1, _barrier1.Object);

            Assert.That(_consumerRepository.GetBarrierFor(_handler1), Is.EqualTo(_barrier1.Object));
        }

        [Test]
        public void ShouldReturnNullForBarrierWhenHandlerIsNotRegistered()
        {
            Assert.That(_consumerRepository.GetBarrierFor(_handler1), Is.Null);
        }

        [Test]
        public void ShouldGetLastEventProcessorsInChain()
        {
            _consumerRepository.Add(_eventProcessor1.Object, _handler1, _barrier1.Object);
            _consumerRepository.Add(_eventProcessor2.Object, _handler2, _barrier2.Object);

            _consumerRepository.UnMarkEventProcessorsAsEndOfChain(_eventProcessor2.Object.Sequence);

            var lastEventProcessorsInChain = _consumerRepository.GetLastSequenceInChain(true);
            Assert.That(lastEventProcessorsInChain.Length, Is.EqualTo(1));
            Assert.That(lastEventProcessorsInChain[0], Is.EqualTo(_eventProcessor1.Object.Sequence));
        }

        [Test]
        public void ShouldRetrieveEventProcessorForHandler()
        {
            _consumerRepository.Add(_eventProcessor1.Object, _handler1, _barrier1.Object);
            Assert.That(_consumerRepository.GetEventProcessorFor(_handler1), Is.EqualTo(_eventProcessor1.Object));
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void ShouldThrowExceptionWhenHandlerIsNotRegistered()
        {
            _consumerRepository.GetEventProcessorFor(new SleepingEventHandler());
        }

        [Test]
        public void ShouldIterateAllEventProcessors()
        {
            _consumerRepository.Add(_eventProcessor1.Object, _handler1, _barrier1.Object);
            _consumerRepository.Add(_eventProcessor2.Object, _handler2, _barrier2.Object);

            var seen1 = false;
            var seen2 = false;
            foreach (var testEntryEventProcessorInfo in _consumerRepository)
            {
                var eventProcessorInfo = (EventProcessorInfo<TestEvent>)testEntryEventProcessorInfo;
                if (!seen1 && eventProcessorInfo.EventProcessor == _eventProcessor1.Object && eventProcessorInfo.Handler == _handler1)
                {
                    seen1 = true;
                }
                else if (!seen2 && eventProcessorInfo.EventProcessor == _eventProcessor2.Object && eventProcessorInfo.Handler == _handler2)
                {
                    seen2 = true;
                }
                else
                {
                    Assert.Fail("Unexpected eventProcessor info: " + testEntryEventProcessorInfo);
                }
            }

            Assert.True(seen1, "Included eventProcessor 1");
            Assert.True(seen2, "Included eventProcessor 2");
        }
    }
}