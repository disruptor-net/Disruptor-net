using System.Linq;
using Disruptor.Tests.Support;
using NUnit.Framework;

#if DISRUPTOR_V5

namespace Disruptor.Tests
{
    [TestFixture]
    public class EventBatchTests
    {
        [Test]
        public void ShouldConvertBatchToArray()
        {
            // Arrange
            var array = new[] { new TestEvent(), new TestEvent(), new TestEvent() };
            var batch = new EventBatch<TestEvent>(array, 1, 2);

            // Act
            var copy = batch.ToArray();

            // Assert
            Assert.AreEqual(array.Skip(1).ToArray(), copy);
        }

        [Test]
        public void ShouldConvertBatchToArrayFromEnumerable()
        {
            // Arrange
            var array = new[] { new TestEvent(), new TestEvent(), new TestEvent() };
            var batch = new EventBatch<TestEvent>(array, 1, 2);

            // Act
            var copy = batch.AsEnumerable().ToArray();

            // Assert
            Assert.AreEqual(array.Skip(1).ToArray(), copy);
        }
    }
}

#endif
