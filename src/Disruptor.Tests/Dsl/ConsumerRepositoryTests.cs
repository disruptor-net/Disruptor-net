using System;
using Disruptor.Dsl;
using Disruptor.Processing;
using Disruptor.Tests.Dsl.Stubs;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests.Dsl;

[TestFixture]
public class ConsumerRepositoryTests
{
    private readonly ConsumerRepository _consumerRepository = new();
    private readonly DummyEventProcessor _eventProcessor1 = new();
    private readonly DummyEventProcessor _eventProcessor2 = new();
    private readonly DummyEventHandler<TestEvent> _handler1 = new();
    private readonly DummyEventHandler<TestEvent> _handler2 = new();
    private readonly DependentSequenceGroup _dependentSequenceGroup1 = new(new Sequence());
    private readonly DependentSequenceGroup _dependentSequenceGroup2 = new(new Sequence());

    [Test]
    public void ShouldGetDependentSequenceGroupByHandler()
    {
        _consumerRepository.AddOwnedProcessor(_eventProcessor1, _handler1, _dependentSequenceGroup1);

        Assert.That(_consumerRepository.GetDependentSequencesFor(_handler1), Is.SameAs(_dependentSequenceGroup1));
    }

    [Test]
    public void ShouldReturnNullForDependentSequenceGroupWhenHandlerIsNotRegistered()
    {
        Assert.That(_consumerRepository.GetDependentSequencesFor(_handler1), Is.Null);
    }

    [Test]
    public void ShouldRetrieveEventProcessorForHandler()
    {
        _consumerRepository.AddOwnedProcessor(_eventProcessor1, _handler1, _dependentSequenceGroup1);
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
        _consumerRepository.AddOwnedProcessor(_eventProcessor1, _handler1, _dependentSequenceGroup1);
        _consumerRepository.AddOwnedProcessor(_eventProcessor2, _handler2, _dependentSequenceGroup2);

        var seen1 = false;
        var seen2 = false;
        foreach (var testEntryEventProcessorInfo in _consumerRepository.Consumers)
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

        Assert.That(seen1, "Included eventProcessor 1");
        Assert.That(seen2, "Included eventProcessor 2");
    }
}
