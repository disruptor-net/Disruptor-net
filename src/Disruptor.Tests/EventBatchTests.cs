using System;
using System.Linq;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests;

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

    [TestCase(0, 3)]
    [TestCase(1, 2)]
    [TestCase(2, 1)]
    [TestCase(3, 0)]
    public void ShouldGetSlice(int start, int length)
    {
        // Arrange
        var array = new[] { new TestEvent(), new TestEvent(), new TestEvent() };
        var batch = new EventBatch<TestEvent>(array, 0, 3);

        // Act
        var slice = batch.Slice(start, length);

        // Assert
        Assert.AreEqual(batch.AsSpan().Slice(start, length).ToArray(), slice.ToArray());
    }

    [TestCase(-1, 3)]
    [TestCase(1, 3)]
    [TestCase(2, 2)]
    [TestCase(3, 1)]
    [TestCase(4, 0)]
    public void ShouldThrowOnInvalidSliceArguments(int start, int length)
    {
        // Arrange
        var array = new[] { new TestEvent(), new TestEvent(), new TestEvent() };
        var batch = new EventBatch<TestEvent>(array, 0, 3);

        // Act/Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => batch.Slice(start, length));
    }
}
