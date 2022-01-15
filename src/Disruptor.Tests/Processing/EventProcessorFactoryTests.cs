using System.Linq;
using Disruptor.Processing;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests.Processing;

[TestFixture]
public class EventProcessorFactoryTests
{
    [Test]
    public void should_create_value_type_proxy_for_each_parameter()
    {
        // Arrange
        var dataProvider = new RingBuffer<TestEvent>(() => new TestEvent(), 64);
        var sequenceBarrier = dataProvider.NewBarrier();

        // Act
        var eventProcessor = EventProcessorFactory.Create(dataProvider, sequenceBarrier, new DummyEventHandler<TestEvent>());

        // Assert
        foreach (var genericArgument in eventProcessor.GetType().GetGenericArguments().Where(x => x != typeof(TestEvent)))
        {
            Assert.True(genericArgument.IsValueType, $"Generic argument {genericArgument.Name} is not a value type");
        }
    }
}