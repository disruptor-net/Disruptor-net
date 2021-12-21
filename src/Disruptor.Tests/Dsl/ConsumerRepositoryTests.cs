using System;
using Disruptor.Dsl;
using Disruptor.Processing;
using Disruptor.Tests.Dsl.Stubs;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests.Dsl
{
    [TestFixture]
    public class ConsumerRepositoryTests
    {
        private readonly ConsumerRepository _consumerRepository;
        private readonly IEventProcessor _eventProcessor1;
        private readonly IEventProcessor _eventProcessor2;
        private readonly DummyEventHandler<TestEvent> _handler1;
        private readonly DummyEventHandler<TestEvent> _handler2;
        private readonly ISequenceBarrier _barrier1;
        private readonly ISequenceBarrier _barrier2;

        public ConsumerRepositoryTests()
        {
            _consumerRepository = new ConsumerRepository();
            _eventProcessor1 = new DummyEventProcessor();
            _eventProcessor2 = new DummyEventProcessor();

            _handler1 = new DummyEventHandler<TestEvent>();
            _handler2 = new DummyEventHandler<TestEvent>();

            _barrier1 = new DummySequenceBarrier();
            _barrier2 = new DummySequenceBarrier();
        }

        [Test]
        public void ShouldGetBarrierByHandler()
        {
            _consumerRepository.Add(_eventProcessor1, _handler1, _barrier1);

            Assert.That(_consumerRepository.GetBarrierFor(_handler1), Is.SameAs(_barrier1));
        }

        [Test]
        public void ShouldReturnNullForBarrierWhenHandlerIsNotRegistered()
        {
            Assert.That(_consumerRepository.GetBarrierFor(_handler1), Is.Null);
        }

        [Test]
        public void ShouldGetLastEventProcessorsInChain()
        {
            _consumerRepository.Add(_eventProcessor1, _handler1, _barrier1);
            _consumerRepository.Add(_eventProcessor2, _handler2, _barrier2);

            _consumerRepository.UnMarkEventProcessorsAsEndOfChain(_eventProcessor2.Sequence);

            var lastEventProcessorsInChain = _consumerRepository.GetLastSequenceInChain(true);
            Assert.That(lastEventProcessorsInChain.Length, Is.EqualTo(1));
            Assert.That(lastEventProcessorsInChain[0], Is.SameAs(_eventProcessor1.Sequence));
        }

        [Test]
        public void ShouldRetrieveEventProcessorForHandler()
        {
            _consumerRepository.Add(_eventProcessor1, _handler1, _barrier1);
            Assert.That(_consumerRepository.GetEventProcessorFor(_handler1), Is.SameAs(_eventProcessor1));
        }

        [Test]
        public void ShouldThrowExceptionWhenHandlerIsNotRegistered()
        {
            Assert.Throws<ArgumentException>(() => _consumerRepository.GetEventProcessorFor(new SleepingEventHandler()));
        }

        [Test]
        public void ShouldIterateAllEventProcessors()
        {
            _consumerRepository.Add(_eventProcessor1, _handler1, _barrier1);
            _consumerRepository.Add(_eventProcessor2, _handler2, _barrier2);

            var seen1 = false;
            var seen2 = false;
            foreach (var testEntryEventProcessorInfo in _consumerRepository)
            {
                var eventProcessorInfo = (EventProcessorInfo)testEntryEventProcessorInfo;
                if (!seen1 && eventProcessorInfo.EventProcessor == _eventProcessor1 && eventProcessorInfo.Handler == _handler1)
                {
                    seen1 = true;
                }
                else if (!seen2 && eventProcessorInfo.EventProcessor == _eventProcessor2 && eventProcessorInfo.Handler == _handler2)
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
