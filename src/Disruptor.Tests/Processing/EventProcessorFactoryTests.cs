using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Disruptor.Processing;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests.Processing;

[TestFixture]
public class EventProcessorFactoryTests
{
    [Test]
    public void ShouldCreateValueTypeProxyForEachParameter()
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

    [Test]
    public void ShouldDetectExplicitImplementation()
    {
        Assert.True(EventProcessorFactory.HasNonDefaultImplementation(typeof(TypeWithImplicitImplementation<int>), typeof(IInterface<int>), nameof(IInterface<int>.Method)));
        Assert.True(EventProcessorFactory.HasNonDefaultImplementation(typeof(TypeWithExplicitImplementation<int>), typeof(IInterface<int>), nameof(IInterface<int>.Method)));
        Assert.False(EventProcessorFactory.HasNonDefaultImplementation(typeof(TypeWithNoImplementation<int>), typeof(IInterface<int>), nameof(IInterface<int>.Method)));
    }

    [SuppressMessage("ReSharper", "UnusedTypeParameter")]
    private interface IInterface<T>
    {
        void Method()
        {
        }
    }

    private class TypeWithImplicitImplementation<T> : IInterface<T>
    {
        public void Method()
        {
        }
    }

    private class TypeWithExplicitImplementation<T> : IInterface<T>
    {
        void IInterface<T>.Method()
        {
        }
    }

    private class TypeWithNoImplementation<T> : IInterface<T>
    {
    }
}
