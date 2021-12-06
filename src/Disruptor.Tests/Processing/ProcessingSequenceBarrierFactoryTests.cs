using System;
using Disruptor.Processing;
using NUnit.Framework;

namespace Disruptor.Tests.Processing
{
    [TestFixture]
    public class ProcessingSequenceBarrierFactoryTests
    {
        [Test]
        public void should_create_value_type_proxy_for_each_parameter()
        {
            // Arrange
            var waitStrategy = new YieldingWaitStrategy();
            var sequencer = new SingleProducerSequencer(64, waitStrategy);
            var cursor = new Sequence();

            // Act
            var sequenceBarrier = ProcessingSequenceBarrierFactory.Create(sequencer, waitStrategy, cursor, Array.Empty<ISequence>());

            // Assert
            foreach (var genericArgument in sequenceBarrier.GetType().GetGenericArguments())
            {
                Assert.True(genericArgument.IsValueType, $"Generic argument {genericArgument.Name} is not a value type");
            }
        }
    }
}
